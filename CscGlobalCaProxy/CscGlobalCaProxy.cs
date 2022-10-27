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
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class CscGlobalCaProxy : BaseCAConnector
    {
        private readonly RequestManager _requestManager;

        public CscGlobalCaProxy()
        {
            _requestManager = new RequestManager();
        }

        private ICscGlobalClient CscGlobalClient { get; set; }
        public bool EnableTemplateSync { get; set; }

        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {

                EventLogger.Trace($"Staring Revoke Method");
                var revokeResponse =
                    Task.Run(async () =>
                            await CscGlobalClient.SubmitRevokeCertificateAsync(caRequestId.Substring(0,36)))
                        .Result; //todo fix to use pipe delimiter

                EventLogger.Trace($"Revoke Response JSON: {JsonConvert.SerializeObject(revokeResponse)}");
                EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                
                var revokeResult=_requestManager.GetRevokeResult(revokeResponse);

                if(revokeResult== Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED))
                {
                    return -1;
                }
                else
                {
                    return revokeResult;
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
            EventLogger.Trace($"Full Sync? {certificateAuthoritySyncInfo.DoFullSync}");
            EventLogger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            try
            {
                var certs = new BlockingCollection<ICertificateResponse>(100);
                CscGlobalClient.SubmitCertificateListRequestAsync(certs, cancelToken);

                foreach (var currentResponseItem in certs.GetConsumingEnumerable(cancelToken))
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        EventLogger.Error("Synchronize was canceled.");
                        break;
                    }

                    try
                    {
                        EventLogger.Trace($"Took Certificate ID {currentResponseItem?.Uuid} from Queue");
                        var certStatus = _requestManager.MapReturnStatus(currentResponseItem?.Status);

                        //Keyfactor sync only seems to work when there is a valid cert and I can only get Active valid certs from Csc Global
                        if (certStatus == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.ISSUED) ||
                            certStatus == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED))
                        {
                            //One click renewal/reissue won't work for this implementation so there is an option to disable it by not syncing back template
                            var productId = "CscGlobal";
                            if (EnableTemplateSync) productId = currentResponseItem?.CertificateType;

                            var fileContent =
                                Encoding.ASCII.GetString(
                                    Convert.FromBase64String(currentResponseItem?.Certificate ?? string.Empty));

                            if (fileContent.Length > 0)
                            {
                                var certData = fileContent.Replace("\r\n", string.Empty);
                                var splitCerts =
                                    certData.Split(new[] {"-----END CERTIFICATE-----", "-----BEGIN CERTIFICATE-----"},
                                        StringSplitOptions.RemoveEmptyEntries);
                                foreach (var cert in splitCerts)
                                    if (!cert.Contains(".crt"))
                                    {
                                        EventLogger.Trace($"Split Cert Value: {cert}");

                                        var currentCert = new X509Certificate2(Encoding.ASCII.GetBytes(cert));
                                        if (!currentCert.Subject.Contains("AAA Certificate Services") &&
                                            !currentCert.Subject.Contains("USERTrust RSA Certification Authority") &&
                                            !currentCert.Subject.Contains("Trusted Secure Certificate Authority 5") && 
                                            !currentCert.Subject.Contains("AddTrust External CA Root") &&
                                            !currentCert.Subject.Contains("Trusted Secure Certificate Authority DV"))
                                            blockingBuffer.Add(new CAConnectorCertificate
                                            {
                                                CARequestID =$"{currentResponseItem?.Uuid}-{currentCert.SerialNumber}",
                                                Certificate = cert,
                                                SubmissionDate = currentResponseItem?.OrderDate == null
                                                    ? Convert.ToDateTime(currentCert.NotBefore)
                                                    : Convert.ToDateTime(currentResponseItem.OrderDate),
                                                Status = certStatus,
                                                ProductID = productId
                                            }, cancelToken);
                                    }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        EventLogger.Error("Synchronize was canceled.");
                        break;
                    }
                }
            }
            catch (AggregateException aggEx)
            {
                EventLogger.Error("Csc Global Synchronize Task failed!");
                EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                // ReSharper disable once PossibleIntendedRethrow
                throw aggEx;
            }

            EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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
            EventLogger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);

            RegistrationRequest enrollmentRequest;
            CAConnectorCertificate priorCert;
            ReissueRequest reissueRequest;
            RenewalRequest renewRequest;

            string uUId;
            switch (enrollmentType)
            {
                case RequestUtilities.EnrollmentType.New:
                    EventLogger.Trace($"Entering New Enrollment");
                    //If they renewed an expired cert it gets here and this will not be supported
                    IRegistrationResponse enrollmentResponse;
                    if (!productInfo.ProductParameters.ContainsKey("PriorCertSN"))
                    {
                        enrollmentRequest = _requestManager.GetRegistrationRequest(productInfo, csr, san);
                        EventLogger.Trace($"Enrollment Request JSON: {JsonConvert.SerializeObject(enrollmentRequest)}");
                        enrollmentResponse =
                            Task.Run(async () => await CscGlobalClient.SubmitRegistrationAsync(enrollmentRequest))
                                .Result;
                        EventLogger.Trace($"Enrollment Response JSON: {JsonConvert.SerializeObject(enrollmentResponse)}");
                    }
                    else
                    {
                        return new EnrollmentResult
                        {
                            Status = 30, //failure
                            StatusMessage = "You cannot renew and expired cert please perform an new enrollment."
                        };
                    }
                    EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                    return _requestManager.GetEnrollmentResult(enrollmentResponse);
                case RequestUtilities.EnrollmentType.Renew:
                    EventLogger.Trace($"Entering Renew Enrollment");
                    //One click won't work for this implementation b/c we are missing enrollment params
                    if (productInfo.ProductParameters.ContainsKey("Applicant Last Name")) 
                    {
                        priorCert = certificateDataReader.GetCertificateRecord(
                            DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                        uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                        EventLogger.Trace($"Renew uUId: {uUId}");
                        renewRequest = _requestManager.GetRenewalRequest(productInfo, uUId, csr, san);
                        EventLogger.Trace($"Renewal Request JSON: {JsonConvert.SerializeObject(renewRequest)}");
                        var renewResponse = Task.Run(async () => await CscGlobalClient.SubmitRenewalAsync(renewRequest))
                            .Result;
                        EventLogger.Trace($"Renewal Response JSON: {JsonConvert.SerializeObject(renewResponse)}");
                        EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                        return _requestManager.GetRenewResponse(renewResponse);
                    }
                    else
                    {
                        return new EnrollmentResult
                        {
                            Status = 30, //failure
                            StatusMessage = "One click Renew Is Not Available for this Certificate Type.  Use the configure button instead."
                        };
                    }


                case RequestUtilities.EnrollmentType.Reissue:
                    EventLogger.Trace($"Entering Reissue Enrollment");
                    //One click won't work for this implementation b/c we are missing enrollment params
                    if (productInfo.ProductParameters.ContainsKey("Applicant Last Name"))
                    {
                        priorCert = certificateDataReader.GetCertificateRecord(
                        DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                        uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                        EventLogger.Trace($"Reissue uUId: {uUId}");
                        reissueRequest = _requestManager.GetReissueRequest(productInfo, uUId, csr, san);
                        EventLogger.Trace($"Reissue JSON: {JsonConvert.SerializeObject(reissueRequest)}");
                        var reissueResponse = Task.Run(async () => await CscGlobalClient.SubmitReissueAsync(reissueRequest))
                            .Result;
                        EventLogger.Trace($"Reissue Response JSON: {JsonConvert.SerializeObject(reissueResponse)}");
                        EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                        return _requestManager.GetReIssueResult(reissueResponse);
                    }
                    else
                    {
                        return new EnrollmentResult
                        {
                            Status = 30, //failure
                            StatusMessage = "One click Renew Is Not Available for this Certificate Type.  Use the configure button instead."
                        };
                    }
            }
            EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return null;
        }
        

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            EventLogger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            var keyfactorCaId = caRequestId.Substring(0, 36); //todo fix to use pipe delimiter
            EventLogger.Trace($"Keyfactor Ca Id: {keyfactorCaId}");
            var certificateResponse =
                Task.Run(async () => await CscGlobalClient.SubmitGetCertificateAsync(keyfactorCaId))
                    .Result;

            EventLogger.Trace($"Single Cert JSON: {JsonConvert.SerializeObject(certificateResponse)}");
            EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return new CAConnectorCertificate
            {
                CARequestID = keyfactorCaId,
                Certificate = certificateResponse.Certificate,
                Status = _requestManager.MapReturnStatus(certificateResponse.Status),
                SubmissionDate = Convert.ToDateTime(certificateResponse.OrderDate)
            };
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            EventLogger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            CscGlobalClient = new CscGlobalClient(configProvider);
            var templateSync = configProvider.CAConnectionData["TemplateSync"].ToString();
            if (templateSync.ToUpper() == "ON") EnableTemplateSync = true;
            EventLogger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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