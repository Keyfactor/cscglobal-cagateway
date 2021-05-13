using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAProxy.AnyGateway;
using CAProxy.AnyGateway.Interfaces;
using CAProxy.AnyGateway.Models;
using CAProxy.Common;
using CSS.Common.Logging;
using CSS.PKI;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Keyfactor.AnyGateway.CscGlobal.Client;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class CscGlobalCaProxy:BaseCAConnector
    {
        private readonly RequestManager _requestManager;

        private ICscGlobalClient CscGlobalClient { get; set; }
        private ICAConnectorConfigProvider ConfigManager { get; set; }
        public string PartnerCode { get; set; }
        public string AuthenticationToken { get; set; }
        public int PageSize { get; set; }

        public CscGlobalCaProxy()
        {
            _requestManager = new RequestManager(this);
        }

        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {

            try
            {
                var requestResponse =
                    Task.Run(async () => await CscGlobalClient.SubmitRevokeCertificateAsync(caRequestId)).Result;

                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);

                return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED);
            }
            catch (Exception e)
            {
                Logger.Error($"An Error has occurred during the revoke process {e.Message}");
                return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED);
            }

        }

        [Obsolete]
        public override void Synchronize(ICertificateDataReader certificateDataReader, BlockingCollection<CertificateRecord> blockingBuffer,
            CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken, string logicalName)
        {
            
        }

        public override void Synchronize(ICertificateDataReader certificateDataReader,
            BlockingCollection<CAConnectorCertificate> blockingBuffer,
            CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken)
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
                    if (certStatus==Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.ISSUED) || certStatus == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED))
                    {

                        string fileContent=Encoding.ASCII.GetString(Convert.FromBase64String(currentResponseItem?.Certificate ?? string.Empty));
                        string fileContent2 = Encoding.UTF8.GetString(Convert.FromBase64String(fileContent)); //Double base64 Encoded for some reason
                        if (fileContent2.Length > 0)
                        {
                                var certData = fileContent2.Replace("\r\n", string.Empty);
                                var splitCerts = certData.Split(new[] { "-----END CERTIFICATE-----", "-----BEGIN CERTIFICATE-----" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (string cert in splitCerts)
                                {
                                    if (!cert.Contains(".crt"))
                                    {
                                        X509Certificate2 currentCert = new X509Certificate2(Encoding.ASCII.GetBytes(cert));
                                        blockingBuffer.Add(new CAConnectorCertificate
                                        {
                                            CARequestID =
                                                $"{currentResponseItem?.Uuid}-{currentCert.SerialNumber}",
                                            Certificate = cert,
                                            SubmissionDate = Convert.ToDateTime(currentResponseItem?.OrderDate),
                                            Status = certStatus,
                                            ProductID = $"{currentResponseItem?.CertificateType}"
                                        });
                                    }
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
        public override EnrollmentResult Enroll(string csr, string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo,
            PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            throw new NotImplementedException();
        }

        public override EnrollmentResult Enroll(ICertificateDataReader certificateDataReader, string csr,
            string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo,
            PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);

            IRegistrationResponse enrollmentResponse = null;
            RegistrationRequest enrollmentRequest;

            switch (enrollmentType)
            {
                case RequestUtilities.EnrollmentType.New:
                    //If they renewed an expired cert it gets here and this will not be supported
                    if (!productInfo.ProductParameters.ContainsKey("PriorCertSN"))
                    {
                        enrollmentRequest = _requestManager.GetRegistrationRequest(productInfo,csr);
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

                    break;
                case RequestUtilities.EnrollmentType.Renew:
                    break;
                case RequestUtilities.EnrollmentType.Reissue:
                    break;
            }

            return GetEnrollmentResult(enrollmentResponse);
        }

        private EnrollmentResult GetEnrollmentResult(IRegistrationResponse registrationResponse)
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
                StatusMessage = $"Order Successfully Created With Order Number {registrationResponse?.Result.CommonName}"
            };
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            throw new NotImplementedException();
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            CscGlobalClient = new CscGlobalClient(configProvider);
        }

        public override void Ping()
        {
            
        }

        public override void ValidateCAConnectionInfo(Dictionary<string, object> connectionInfo)
        {
            
        }

        public override void ValidateProductInfo(EnrollmentProductInfo productInfo, Dictionary<string, object> connectionInfo)
        {
            
        }

    }
}
