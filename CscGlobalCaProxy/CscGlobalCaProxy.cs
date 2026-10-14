using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CAProxy.AnyGateway;
using CAProxy.AnyGateway.Interfaces;
using CAProxy.AnyGateway.Models;
using CAProxy.Common;
using CSS.Common;
using CSS.Common.Logging;
using CSS.PKI;
using Keyfactor.AnyGateway.CscGlobal.Client;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class CscGlobalCaProxy : BaseCAConnector
    {
        private const int DefaultRenewalWindowDays = 30;
        private const int CscRequestIdLength = 36; //CARequestID starts with the certificate UUID (a GUID)
        private const int FailedStatus = 30;

        //Parameters every registration/renewal/reissue request reads from the product parameters
        private static readonly string[] RequiredEnrollmentParameters =
        {
            "Term",
            "Applicant First Name",
            "Applicant Last Name",
            "Applicant Email Address",
            "Applicant Phone (+nn.nnnnnnnn)",
            "Organization Contact",
            "Business Unit",
            "Domain Control Validation Method",
            "CN DCV Email (admin@yourdomain.com)",
            "Notification Email(s) Comma Separated"
        };

        private static readonly string[] CscDateFormats = {"dd-MMM-yyyy", "d-MMM-yyyy"};

        private readonly RequestManager _requestManager;

        public CscGlobalCaProxy()
        {
            _requestManager = new RequestManager();
        }

        private int RenewalWindowDays { get; set; } = DefaultRenewalWindowDays;
        private ICscGlobalClient CscGlobalClient { get; set; }
        public bool EnableTemplateSync { get; set; }

        private FlowLogger NewFlow(string name)
        {
            return new FlowLogger(message => Logger.Trace(message), name);
        }

        private static T RunSync<T>(Func<Task<T>> operation)
        {
            //GetAwaiter().GetResult() surfaces the real exception instead of an AggregateException
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private static EnrollmentResult Failure(string message)
        {
            return new EnrollmentResult
            {
                Status = FailedStatus,
                StatusMessage = message
            };
        }

        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {
            using (var flow = NewFlow("Revoke"))
            {
                try
                {
                    Logger.Trace("Starting Revoke Method");
                    if (string.IsNullOrEmpty(caRequestId) || caRequestId.Length < CscRequestIdLength)
                        throw new ArgumentException(
                            $"The CA request ID '{caRequestId}' is not valid. Expected it to start with the {CscRequestIdLength} character certificate UUID.");

                    var uuid = caRequestId.Substring(0, CscRequestIdLength); //todo fix to use pipe delimiter
                    flow.Step("ParseCaRequestId", $"uuid={uuid}");

                    var revokeResponse = flow.Step("SubmitRevokeToCsc",
                        () => RunSync(() => CscGlobalClient.SubmitRevokeCertificateAsync(uuid)));
                    Logger.Trace($"Revoke Response JSON: {JsonConvert.SerializeObject(revokeResponse)}");

                    var revokeResult = _requestManager.GetRevokeResult(revokeResponse);
                    flow.Step("MapRevokeResult", $"status={revokeResult}");

                    if (revokeResult == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED) &&
                        !string.IsNullOrEmpty(revokeResponse?.RegistrationError?.Description))
                    {
                        flow.Fail("RevokeRejectedByCsc", revokeResponse.RegistrationError.Description);
                        throw new UnsuccessfulRequestException(
                            $"Revoke Failed with message {revokeResponse.RegistrationError.Description}", 30);
                    }

                    return revokeResult;
                }
                catch (UnsuccessfulRequestException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Error($"Revoke failed: {LogHandler.FlattenException(e)}");
                    throw new Exception($"Revoke Failed with message {e.Message}", e);
                }
                finally
                {
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                }
            }
        }

        [Obsolete]
        public override void Synchronize(ICertificateDataReader certificateDataReader,
            BlockingCollection<CertificateRecord> blockingBuffer,
            CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken,
            string logicalName)
        {
        }

        public override void Synchronize(ICertificateDataReader certificateDataReader,
            BlockingCollection<CAConnectorCertificate> blockingBuffer,
            CertificateAuthoritySyncInfo certificateAuthoritySyncInfo,
            CancellationToken cancelToken)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            using (var flow = NewFlow("Synchronize"))
            {
                try
                {
                    var fullSync = certificateAuthoritySyncInfo?.DoFullSync ?? true;
                    Logger.Trace($"Full Sync? {fullSync}");

                    CertificateListResponse certs;
                    if (fullSync)
                    {
                        flow.Step("SelectSyncMode", "full");
                        certs = flow.Step("FetchCertificateList",
                            () => RunSync(() => CscGlobalClient.SubmitCertificateListRequestAsync()));
                    }
                    else
                    {
                        // Incremental sync - get certificates from the last X days (default 5)
                        var effectiveDate = DateTime.Now.AddDays(-Constants.IncrementalSyncDays);
                        flow.Step("SelectSyncMode", $"incremental, effectiveDate={effectiveDate:yyyy/MM/dd}");
                        certs = flow.Step("FetchCertificateList",
                            () => RunSync(() => CscGlobalClient.SubmitIncrementalCertificateListRequestAsync(effectiveDate)));
                    }

                    var results = certs?.Results ?? new List<CertificateResponse>();
                    flow.Step("ReceivedCertificates", $"count={results.Count}");

                    int added = 0, skipped = 0, failed = 0;
                    foreach (var currentResponseItem in results)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        var uuid = currentResponseItem?.Uuid;
                        try
                        {
                            Logger.Trace($"Took Certificate ID {uuid} from Queue");
                            var certStatus = _requestManager.MapReturnStatus(currentResponseItem?.Status);

                            //Keyfactor sync only seems to work when there is a valid cert and I can only get Active valid certs from Csc Global
                            if (certStatus != Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.ISSUED) &&
                                certStatus != Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED))
                            {
                                Logger.Trace($"Skipping {uuid}: status '{currentResponseItem?.Status}' is not issued or revoked");
                                skipped++;
                                continue;
                            }

                            //One click renewal/reissue won't work for this implementation so there is an option to disable it by not syncing back template
                            var productId = "CscGlobal";
                            if (EnableTemplateSync) productId = currentResponseItem?.CertificateType;

                            var fileContent =
                                Encoding.ASCII.GetString(
                                    Convert.FromBase64String(currentResponseItem?.Certificate ?? string.Empty));
                            if (fileContent.Length == 0)
                            {
                                Logger.Trace($"Skipping {uuid}: no certificate content returned");
                                skipped++;
                                continue;
                            }

                            Logger.Trace($"File Content {fileContent}");
                            var certString = GetEndEntityCertificate(fileContent.Replace("\r\n", string.Empty));
                            if (string.IsNullOrEmpty(certString))
                            {
                                Logger.Trace($"Skipping {uuid}: no end entity certificate found");
                                skipped++;
                                continue;
                            }

                            var currentCert = new X509Certificate2(Encoding.ASCII.GetBytes(certString));
                            blockingBuffer.Add(new CAConnectorCertificate
                            {
                                CARequestID = $"{uuid}",
                                Certificate = certString,
                                SubmissionDate = currentResponseItem?.OrderDate == null
                                    ? Convert.ToDateTime(currentCert.NotBefore)
                                    : Convert.ToDateTime(currentResponseItem.OrderDate),
                                Status = certStatus,
                                ProductID = productId
                            }, cancelToken);
                            added++;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception e)
                        {
                            //One bad certificate should not abort the whole sync
                            failed++;
                            Logger.Error($"Failed to process certificate {uuid} during sync, continuing: {LogHandler.FlattenException(e)}");
                        }
                    }

                    flow.Step("ProcessCertificates", $"added={added}, skipped={skipped}, failed={failed}");
                }
                catch (Exception e)
                {
                    flow.Fail("Synchronize", e.Message);
                    Logger.Error($"Csc Global Synchronize Task failed! {LogHandler.FlattenException(e)}");
                    throw;
                }
                finally
                {
                    blockingBuffer.CompleteAdding();
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                }
            }
        }

        private string GetEndEntityCertificate(string certData)
        {
            var splitCerts =
                certData.Split(new[] {"-----END CERTIFICATE-----", "-----BEGIN CERTIFICATE-----"},
                    StringSplitOptions.RemoveEmptyEntries);

            X509Certificate2Collection col = new X509Certificate2Collection();
            foreach (var cert in splitCerts)
            {
                Logger.Trace($"Split Cert Value: {cert}");

                //skip these headers that came with the split function
                if (!cert.Contains(".crt"))
                {
                    col.Import(Encoding.UTF8.GetBytes(cert));
                }
            }

            Logger.Trace("Getting End Entity Certificate");
            var currentCert = CSS.PKI.X509.X509Utilities.GetEndEntityCertificate(col);
            Logger.Trace("Converting to Byte Array");
            var byteArray = currentCert?.Export(X509ContentType.Cert);
            Logger.Trace("Initializing empty string");

            var certString = string.Empty;
            if (byteArray != null)
            {
                certString = Convert.ToBase64String(byteArray);
            }

            Logger.Trace($"Got certificate {certString}");

            return certString;
        }

        [Obsolete]
        public override EnrollmentResult Enroll(string csr, string subject, Dictionary<string, string[]> san,
            EnrollmentProductInfo productInfo,
            PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            return null;
        }

        public override EnrollmentResult Enroll(ICertificateDataReader certificateDataReader, string csr,
            string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo,
            PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            using (var flow = NewFlow($"Enroll-{enrollmentType}"))
            {
                try
                {
                    return EnrollInternal(flow, certificateDataReader, csr, san, productInfo, enrollmentType);
                }
                catch (Exception e)
                {
                    flow.Fail("Enroll", e.Message);
                    Logger.Error($"Enrollment ({enrollmentType}) failed: {LogHandler.FlattenException(e)}");
                    return Failure($"{enrollmentType} failed: {e.Message}");
                }
                finally
                {
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                }
            }
        }

        private EnrollmentResult EnrollInternal(FlowLogger flow, ICertificateDataReader certificateDataReader,
            string csr, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo,
            RequestUtilities.EnrollmentType enrollmentType)
        {
            if (productInfo?.ProductParameters == null)
            {
                flow.Fail("ValidateInput", "no product parameters supplied");
                return Failure("The enrollment request did not include any product parameters.");
            }

            if (string.IsNullOrWhiteSpace(csr))
            {
                flow.Fail("ValidateInput", "no CSR supplied");
                return Failure("The enrollment request did not include a certificate signing request (CSR).");
            }

            flow.Step("ValidateInput", $"product={productInfo.ProductID}, hasPriorCert={productInfo.ProductParameters.ContainsKey("PriorCertSN")}");

            // If the order is outside the renewal window, reissue under the active order instead of a paid renew
            if (enrollmentType == RequestUtilities.EnrollmentType.Renew)
            {
                if (ShouldReissueInsteadOfRenew(flow, certificateDataReader, productInfo))
                {
                    flow.Step("RenewalDecision", "REISSUE (order outside renewal window)");
                    enrollmentType = RequestUtilities.EnrollmentType.Reissue;
                }
                else
                {
                    flow.Step("RenewalDecision", "RENEWAL (paid order)");
                }
            }

            switch (enrollmentType)
            {
                case RequestUtilities.EnrollmentType.New:
                    return EnrollNew(flow, csr, san, productInfo);
                case RequestUtilities.EnrollmentType.Renew:
                    return EnrollRenew(flow, certificateDataReader, csr, san, productInfo);
                case RequestUtilities.EnrollmentType.Reissue:
                    return EnrollReissue(flow, certificateDataReader, csr, san, productInfo);
                default:
                    flow.Fail("SelectPath", $"unsupported enrollment type {enrollmentType}");
                    return Failure($"The enrollment type '{enrollmentType}' is not supported by the CSC Global gateway.");
            }
        }

        private EnrollmentResult EnrollNew(FlowLogger flow, string csr, Dictionary<string, string[]> san,
            EnrollmentProductInfo productInfo)
        {
            flow.Step("SelectPath", "New");

            //A renewal of an expired certificate reaches here as a New enrollment carrying PriorCertSN. CSC has no
            //order to renew in that case, so it is simply ordered as a new certificate.
            if (productInfo.ProductParameters.ContainsKey("PriorCertSN"))
                flow.Step("PriorCertIgnored", "expired certificate renewal is ordered as a new certificate");

            var missing = ValidateRequiredParameters(flow, productInfo);
            if (missing != null) return Failure(missing);

            var enrollmentRequest = flow.Step("BuildRegistrationRequest",
                () => _requestManager.GetRegistrationRequest(productInfo, csr, san));
            Logger.Trace($"Enrollment Request JSON: {JsonConvert.SerializeObject(enrollmentRequest)}");

            var enrollmentResponse = flow.Step("SubmitRegistrationToCsc",
                () => RunSync(() => CscGlobalClient.SubmitRegistrationAsync(enrollmentRequest)));
            if (enrollmentResponse == null)
                throw new InvalidOperationException("CSC Global returned an empty response to the registration request.");
            Logger.Trace($"Enrollment Response JSON: {JsonConvert.SerializeObject(enrollmentResponse)}");

            var result = _requestManager.GetEnrollmentResult(enrollmentResponse);
            flow.Step("MapEnrollmentResult", $"status={result?.Status}");
            return result;
        }

        private EnrollmentResult EnrollRenew(FlowLogger flow, ICertificateDataReader certificateDataReader,
            string csr, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo)
        {
            flow.Step("SelectPath", "Renew");

            var missing = ValidateRequiredParameters(flow, productInfo, true);
            if (missing != null) return Failure(missing);

            var uUId = GetPriorCertificateUuid(flow, certificateDataReader, productInfo);
            var renewRequest = flow.Step("BuildRenewalRequest",
                () => _requestManager.GetRenewalRequest(productInfo, uUId, csr, san));
            Logger.Trace($"Renewal Request JSON: {JsonConvert.SerializeObject(renewRequest)}");

            var renewResponse = flow.Step("SubmitRenewalToCsc",
                () => RunSync(() => CscGlobalClient.SubmitRenewalAsync(renewRequest)));
            if (renewResponse == null)
                throw new InvalidOperationException("CSC Global returned an empty response to the renewal request.");
            Logger.Trace($"Renewal Response JSON: {JsonConvert.SerializeObject(renewResponse)}");

            var result = _requestManager.GetRenewResponse(renewResponse);
            flow.Step("MapRenewalResult", $"status={result?.Status}");
            return result;
        }

        private EnrollmentResult EnrollReissue(FlowLogger flow, ICertificateDataReader certificateDataReader,
            string csr, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo)
        {
            flow.Step("SelectPath", "Reissue");

            var missing = ValidateRequiredParameters(flow, productInfo, true);
            if (missing != null) return Failure(missing);

            var uUId = GetPriorCertificateUuid(flow, certificateDataReader, productInfo);
            var reissueRequest = flow.Step("BuildReissueRequest",
                () => _requestManager.GetReissueRequest(productInfo, uUId, csr, san));
            Logger.Trace($"Reissue JSON: {JsonConvert.SerializeObject(reissueRequest)}");

            var reissueResponse = flow.Step("SubmitReissueToCsc",
                () => RunSync(() => CscGlobalClient.SubmitReissueAsync(reissueRequest)));
            if (reissueResponse == null)
                throw new InvalidOperationException("CSC Global returned an empty response to the reissue request.");
            Logger.Trace($"Reissue Response JSON: {JsonConvert.SerializeObject(reissueResponse)}");

            var result = _requestManager.GetReIssueResult(reissueResponse);
            flow.Step("MapReissueResult", $"status={result?.Status}");
            return result;
        }

        /// <summary>
        /// Returns a failure message when required enrollment parameters are missing, otherwise null.
        /// </summary>
        private string ValidateRequiredParameters(FlowLogger flow, EnrollmentProductInfo productInfo, bool priorCertRequired = false)
        {
            var parameters = productInfo.ProductParameters;
            var missing = RequiredEnrollmentParameters
                .Where(name => !parameters.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
                .ToList();

            if (missing.Count == 0)
            {
                flow.Step("ValidateParameters");
                return null;
            }

            flow.Fail("ValidateParameters", $"missing: {string.Join(", ", missing)}");
            var message = $"The enrollment request is missing required parameter(s): {string.Join(", ", missing)}.";
            if (priorCertRequired)
                message += " One-click renew/reissue does not supply these fields for this certificate type; use the configure option to provide them.";
            return message;
        }

        private string GetPriorCertificateUuid(FlowLogger flow, ICertificateDataReader certificateDataReader,
            EnrollmentProductInfo productInfo)
        {
            if (!productInfo.ProductParameters.TryGetValue("PriorCertSN", out var priorCertSn) ||
                string.IsNullOrWhiteSpace(priorCertSn))
                throw new InvalidOperationException("The request does not identify the prior certificate (PriorCertSN is missing).");

            var priorCert = certificateDataReader.GetCertificateRecord(DataConversion.HexToBytes(priorCertSn));
            if (priorCert == null)
                throw new InvalidOperationException($"The prior certificate with serial number '{priorCertSn}' was not found in the gateway database.");

            var requestId = priorCert.CARequestID;
            if (string.IsNullOrEmpty(requestId) || requestId.Length < CscRequestIdLength)
                throw new InvalidOperationException($"The prior certificate's CA request ID '{requestId}' is not a valid CSC Global certificate ID.");

            var uUId = requestId.Substring(0, CscRequestIdLength); //uUId is a GUID
            flow.Step("ResolvePriorCertificate", $"uuid={uUId}");
            return uUId;
        }

        /// <summary>
        /// Decides renew vs. reissue from the order's remaining validity (order date + 1 year) rather than the
        /// certificate's own expiration, so shorter-lived certs under an active order are reissued, not renewed.
        /// Returns true when the order is outside the renewal window (reissue). Falls back to the certificate's
        /// expiration when the order date is unavailable, and to renewal if nothing can be determined.
        /// </summary>
        private bool ShouldReissueInsteadOfRenew(FlowLogger flow, ICertificateDataReader certificateDataReader,
            EnrollmentProductInfo productInfo)
        {
            try
            {
                if (!productInfo.ProductParameters.TryGetValue("PriorCertSN", out var priorCertSn) ||
                    string.IsNullOrWhiteSpace(priorCertSn))
                {
                    flow.Skip("EvaluateOrderValidity", "no PriorCertSN");
                    return false;
                }

                var priorCert = certificateDataReader.GetCertificateRecord(DataConversion.HexToBytes(priorCertSn));
                var requestId = priorCert?.CARequestID;
                if (string.IsNullOrEmpty(requestId) || requestId.Length < CscRequestIdLength)
                {
                    flow.Skip("EvaluateOrderValidity", "prior certificate or its CA request ID not found");
                    return false;
                }

                var uUId = requestId.Substring(0, CscRequestIdLength);
                var certificate = flow.Step("FetchLiveCertificate",
                    () => RunSync(() => CscGlobalClient.SubmitGetCertificateAsync(uUId)), $"uuid={uUId}");

                if (TryParseCscDate(certificate?.OrderDate, out var orderDate))
                {
                    var orderExpiry = orderDate.AddYears(1);
                    var daysUntilOrderExpiry = (orderExpiry - DateTime.UtcNow).TotalDays;
                    var reissue = daysUntilOrderExpiry > RenewalWindowDays;
                    flow.Step("EvaluateOrderValidity",
                        $"orderDate={certificate.OrderDate}, orderExpiry={orderExpiry:dd-MMM-yyyy}, daysLeft={(int) daysUntilOrderExpiry}, window={RenewalWindowDays}, reissue={reissue}");
                    return reissue;
                }

                if (TryParseCscDate(certificate?.ExpirationDate, out var expirationDate))
                {
                    var reissue = expirationDate >= DateTime.UtcNow;
                    flow.Step("EvaluateOrderValidity",
                        $"order date unavailable, using certificate expiration {certificate.ExpirationDate}, reissue={reissue}");
                    return reissue;
                }

                flow.Skip("EvaluateOrderValidity", "neither order date nor expiration date available");
                return false;
            }
            catch (Exception e)
            {
                flow.Fail("EvaluateOrderValidity", e.Message);
                Logger.Warn($"Unable to determine order validity, proceeding with renewal: {e.Message}");
                return false;
            }
        }

        private static bool TryParseCscDate(string value, out DateTime date)
        {
            const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
            if (DateTime.TryParseExact(value, CscDateFormats, CultureInfo.InvariantCulture, styles, out date))
                return true;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, styles, out date);
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            using (var flow = NewFlow("GetSingleRecord"))
            {
                try
                {
                    if (string.IsNullOrEmpty(caRequestId) || caRequestId.Length < CscRequestIdLength)
                        throw new ArgumentException(
                            $"The CA request ID '{caRequestId}' is not valid. Expected it to start with the {CscRequestIdLength} character certificate UUID.");

                    var keyfactorCaId = caRequestId.Substring(0, CscRequestIdLength); //todo fix to use pipe delimiter
                    flow.Step("ParseCaRequestId", $"uuid={keyfactorCaId}");

                    var certificateResponse = flow.Step("FetchCertificateFromCsc",
                        () => RunSync(() => CscGlobalClient.SubmitGetCertificateAsync(keyfactorCaId)));
                    Logger.Trace($"Single Cert JSON: {JsonConvert.SerializeObject(certificateResponse)}");

                    var fileContent =
                        Encoding.ASCII.GetString(
                            Convert.FromBase64String(certificateResponse?.Certificate ?? string.Empty));

                    Logger.Trace($"File Content {fileContent}");
                    var certData = fileContent.Replace("\r\n", string.Empty);
                    var certString = string.Empty;
                    if (!string.IsNullOrEmpty(certData))
                        certString = flow.Step("ExtractEndEntityCertificate", () => GetEndEntityCertificate(certData));
                    else
                        flow.Skip("ExtractEndEntityCertificate", "no certificate content returned");
                    Logger.Trace($"Cert String Content {certString}");

                    return new CAConnectorCertificate
                    {
                        CARequestID = keyfactorCaId,
                        Certificate = certString,
                        Status = _requestManager.MapReturnStatus(certificateResponse?.Status),
                        SubmissionDate = Convert.ToDateTime(certificateResponse?.OrderDate)
                    };
                }
                catch (Exception e)
                {
                    flow.Fail("GetSingleRecord", e.Message);
                    Logger.Error($"Error getting single record: {LogHandler.FlattenException(e)}");
                    throw new Exception($"Error Occurred getting single cert {e.Message}", e);
                }
                finally
                {
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                }
            }
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            using (var flow = NewFlow("Initialize"))
            {
                try
                {
                    var connectionData = configProvider?.CAConnectionData
                                         ?? throw new InvalidOperationException("The CA connection configuration is missing.");

                    CscGlobalClient = flow.Step("CreateClient", () => new CscGlobalClient(configProvider));

                    EnableTemplateSync = connectionData.TryGetValue("TemplateSync", out var templateSync) &&
                                         string.Equals(templateSync?.ToString(), "ON", StringComparison.OrdinalIgnoreCase);
                    flow.Step("ReadTemplateSync", $"enabled={EnableTemplateSync}");

                    RenewalWindowDays = DefaultRenewalWindowDays;
                    if (connectionData.TryGetValue("RenewalWindowDays", out var renewalWindow))
                    {
                        if (int.TryParse(renewalWindow?.ToString(), out var renewalWindowDays) && renewalWindowDays > 0)
                        {
                            RenewalWindowDays = renewalWindowDays;
                        }
                        else
                        {
                            Logger.Warn($"RenewalWindowDays value '{renewalWindow}' could not be parsed or was <= 0, using default {DefaultRenewalWindowDays}.");
                            flow.Fail("ReadRenewalWindowDays", $"invalid value, using default {DefaultRenewalWindowDays}");
                        }
                    }

                    flow.Step("ReadRenewalWindowDays", $"days={RenewalWindowDays}");
                }
                catch (Exception e)
                {
                    flow.Fail("Initialize", e.Message);
                    Logger.Error($"Initialization failed: {LogHandler.FlattenException(e)}");
                    throw;
                }
                finally
                {
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                }
            }
        }

        public override void Ping()
        {
        }

        public override void ValidateCAConnectionInfo(Dictionary<string, object> connectionInfo)
        {
        }

        public override void ValidateProductInfo(EnrollmentProductInfo productInfo,
            Dictionary<string, object> connectionInfo)
        {
        }
    }
}
