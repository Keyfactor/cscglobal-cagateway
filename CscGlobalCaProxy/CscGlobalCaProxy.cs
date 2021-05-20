using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class CscGlobalCaProxy : BaseCAConnector
    {
        private readonly RequestManager _requestManager;

        public CscGlobalCaProxy()
        {
            _requestManager = new RequestManager(this);
        }

        private ICscGlobalClient CscGlobalClient { get; set; }
        public bool EnableTemplateSync { get; set; }

        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {
            try
            {
                var revokeResponse =
                    Task.Run(async () =>
                            await CscGlobalClient.SubmitRevokeCertificateAsync(caRequestId.Substring(0, 36)))
                        .Result; //todo fix to use pipe delimiter

                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);

                return GetRevokeResult(revokeResponse);
            }
            catch (Exception e)
            {
                Logger.Error($"An Error has occurred during the revoke process {e.Message}");
                return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED);
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
            try
            {
                var certs = new BlockingCollection<ICertificateResponse>(100);
                CscGlobalClient.SubmitCertificateListRequestAsync(certs, cancelToken);

                foreach (var currentResponseItem in certs.GetConsumingEnumerable(cancelToken))
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        Logger.Error("Synchronize was canceled.");
                        break;
                    }

                    try
                    {
                        Logger.Trace($"Took Certificate ID {currentResponseItem?.Uuid} from Queue");
                        var certStatus = _requestManager.MapReturnStatus(currentResponseItem?.Status);

                        //Keyfactor sync only seems to work when there is a valid cert and I can only get Active valid certs from SSLStore
                        if (certStatus == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.ISSUED) ||
                            certStatus == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED))
                        {
                            //One click renewal/reissue won't work for this implementation so there is an option to disable it by not syncing back template
                            var productId = "CscGlobal";
                            if (EnableTemplateSync) productId = currentResponseItem?.CertificateType;

                            var fileContent =
                                Encoding.ASCII.GetString(
                                    Convert.FromBase64String(currentResponseItem?.Certificate ?? string.Empty));
                            var fileContent2 =
                                Encoding.UTF8.GetString(
                                    Convert.FromBase64String(fileContent)); //Double base64 Encoded for some reason
                            if (fileContent2.Length > 0)
                            {
                                var certData = fileContent2.Replace("\r\n", string.Empty);
                                var splitCerts =
                                    certData.Split(new[] {"-----END CERTIFICATE-----", "-----BEGIN CERTIFICATE-----"},
                                        StringSplitOptions.RemoveEmptyEntries);
                                foreach (var cert in splitCerts)
                                    if (!cert.Contains(".crt"))
                                    {
                                        var currentCert = new X509Certificate2(Encoding.ASCII.GetBytes(cert));
                                        if (!currentCert.Subject.Contains("AAA Certificate Services") &&
                                            !currentCert.Subject.Contains("USERTrust RSA Certification Authority") &&
                                            !currentCert.Subject.Contains("Trusted Secure Certificate Authority 5"))
                                            blockingBuffer.Add(new CAConnectorCertificate
                                            {
                                                CARequestID =
                                                    $"{currentResponseItem?.Uuid}-{currentCert.SerialNumber}",
                                                Certificate = cert,
                                                SubmissionDate = Convert.ToDateTime(currentResponseItem?.OrderDate),
                                                Status = certStatus,
                                                ProductID = productId
                                            });
                                    }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Error("Synchronize was canceled.");
                        break;
                    }
                }
            }
            catch (AggregateException aggEx)
            {
                Logger.Error("SslStore Synchronize Task failed!");
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                // ReSharper disable once PossibleIntendedRethrow
                throw aggEx;
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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

            RegistrationRequest enrollmentRequest;
            CAConnectorCertificate priorCert;
            ReissueRequest reissueRequest;
            RenewalRequest renewRequest;

            string uUId;
            switch (enrollmentType)
            {
                case RequestUtilities.EnrollmentType.New:
                    //If they renewed an expired cert it gets here and this will not be supported
                    IRegistrationResponse enrollmentResponse;
                    if (!productInfo.ProductParameters.ContainsKey("PriorCertSN"))
                    {
                        enrollmentRequest = _requestManager.GetRegistrationRequest(productInfo, csr, san);
                        enrollmentResponse =
                            Task.Run(async () => await CscGlobalClient.SubmitRegistrationAsync(enrollmentRequest))
                                .Result;
                    }
                    else
                    {
                        return new EnrollmentResult
                        {
                            Status = 30, //failure
                            StatusMessage = "You cannot renew and expired cert please perform an new enrollment."
                        };
                    }

                    return GetEnrollmentResult(enrollmentResponse);
                case RequestUtilities.EnrollmentType.Renew:
                    priorCert = certificateDataReader.GetCertificateRecord(
                        DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                    uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                    renewRequest = _requestManager.GetRenewalRequest(productInfo, uUId, csr, san);
                    var renewResponse = Task.Run(async () => await CscGlobalClient.SubmitRenewalAsync(renewRequest))
                        .Result;
                    return GetRenewResponse(renewResponse);

                case RequestUtilities.EnrollmentType.Reissue:
                    priorCert = certificateDataReader.GetCertificateRecord(
                        DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                    uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                    reissueRequest = _requestManager.GetReissueRequest(productInfo, uUId, csr, san);
                    var reissueResponse = Task.Run(async () => await CscGlobalClient.SubmitReissueAsync(reissueRequest))
                        .Result;
                    return GetReIssueResult(reissueResponse);
            }

            return null;
        }

        private EnrollmentResult GetRenewResponse(RenewalResponse renewResponse)
        {
            if (renewResponse.RegistrationError != null)
            {
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    StatusMessage = renewResponse.RegistrationError.Description
                };
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return new EnrollmentResult
            {
                Status = 9, //success
                StatusMessage = $"Renewal Successfully Completed For {renewResponse.Result.CommonName}"
            };
        }

        private EnrollmentResult
            GetEnrollmentResult(
                IRegistrationResponse registrationResponse)
        {
            if (registrationResponse.RegistrationError != null)
            {
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    StatusMessage = registrationResponse.RegistrationError.Description
                };
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return new EnrollmentResult
            {
                Status = 9, //success
                StatusMessage =
                    $"Order Successfully Created With Order Number {registrationResponse.Result.CommonName}"
            };
        }

        private int GetRevokeResult(IRevokeResponse revokeResponse)
        {
            if (revokeResponse.RegistrationError != null)
            {
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED);
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED);
        }

        private EnrollmentResult GetReIssueResult(IReissueResponse reissueResponse)
        {
            if (reissueResponse.RegistrationError != null)
            {
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    StatusMessage = reissueResponse.RegistrationError.Description
                };
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return new EnrollmentResult
            {
                Status = 9, //success
                StatusMessage = $"Reissue Successfully Completed For {reissueResponse.Result.CommonName}"
            };
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            var keyfactorCaId = caRequestId.Substring(38); //todo fix to use pipe delimiter
            var certificateResponse =
                Task.Run(async () => await CscGlobalClient.SubmitGetCertificateAsync(caRequestId.Substring(0, 36)))
                    .Result;

            return new CAConnectorCertificate
            {
                CARequestID = keyfactorCaId,
                Certificate = certificateResponse.Certificate,
                ResolutionDate = Convert.ToDateTime(certificateResponse.EffectiveDate),
                Status = _requestManager.MapReturnStatus(certificateResponse.Status),
                SubmissionDate = Convert.ToDateTime(certificateResponse.OrderDate)
            };
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            CscGlobalClient = new CscGlobalClient(configProvider);
            var templateSync = configProvider.CAConnectionData["TemplateSync"].ToString();
            if (templateSync.ToUpper() == "ON") EnableTemplateSync = true;
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