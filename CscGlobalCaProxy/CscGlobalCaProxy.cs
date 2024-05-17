using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly RequestManager _requestManager;

        public CscGlobalCaProxy()
        {
            _requestManager = new RequestManager();
        }

        private ICscGlobalClient CscGlobalClient { get; set; }
        public bool EnableTemplateSync { get; set; }

        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {

            Logger.Trace($"Staring Revoke Method");
            var revokeResponse =
                Task.Run(async () =>
                        await CscGlobalClient.SubmitRevokeCertificateAsync(caRequestId.Substring(0, 36)))
                    .Result; //todo fix to use pipe delimiter

            Logger.Trace($"Revoke Response JSON: {JsonConvert.SerializeObject(revokeResponse)}");
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);

            var revokeResult = _requestManager.GetRevokeResult(revokeResponse);

            if (revokeResult == Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED))
            {
                return -1;
            }

            return revokeResult;

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
            Logger.Trace($"Full Sync? {certificateAuthoritySyncInfo.DoFullSync}");
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            try
            {
                if (certificateAuthoritySyncInfo.DoFullSync)
                {
                    var certs = Task.Run(async () => await CscGlobalClient.SubmitCertificateListRequestAsync()).Result;

                    foreach (var currentResponseItem in certs.Results)
                    {

                        cancelToken.ThrowIfCancellationRequested();
                        Logger.Trace($"Took Certificate ID {currentResponseItem?.Uuid} from Queue");
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
                                var certCollection=GetCertificateChain(fileContent);
                                var leafItem = certCollection.Select(cert => cert.type = "leaf").FirstOrDefault();
                                if (leafItem != null)
                                {
                                    Logger.Trace($"Leaf Cert Value: {leafItem}");

                                    var currentCert = new X509Certificate2(Encoding.ASCII.GetBytes(leafItem));
                                    blockingBuffer.Add(new CAConnectorCertificate
                                    {
                                        CARequestID = $"{currentResponseItem?.Uuid}",
                                        Certificate = leafItem,
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
                    blockingBuffer.CompleteAdding();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Csc Global Synchronize Task failed! {LogHandler.FlattenException(e)}");
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                blockingBuffer.CompleteAdding();
                throw;
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
                    Logger.Trace($"Entering New Enrollment");
                    //If they renewed an expired cert it gets here and this will not be supported
                    IRegistrationResponse enrollmentResponse;
                    if (!productInfo.ProductParameters.ContainsKey("PriorCertSN"))
                    {
                        enrollmentRequest = _requestManager.GetRegistrationRequest(productInfo, csr, san);
                        Logger.Trace($"Enrollment Request JSON: {JsonConvert.SerializeObject(enrollmentRequest)}");
                        enrollmentResponse =
                            Task.Run(async () => await CscGlobalClient.SubmitRegistrationAsync(enrollmentRequest))
                                .Result;
                        Logger.Trace($"Enrollment Response JSON: {JsonConvert.SerializeObject(enrollmentResponse)}");
                    }
                    else
                    {
                        return new EnrollmentResult
                        {
                            Status = 30, //failure
                            StatusMessage = "You cannot renew and expired cert please perform an new enrollment."
                        };
                    }
                    Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                    return _requestManager.GetEnrollmentResult(enrollmentResponse);
                case RequestUtilities.EnrollmentType.Renew:
                    Logger.Trace($"Entering Renew Enrollment");
                    //One click won't work for this implementation b/c we are missing enrollment params
                    if (productInfo.ProductParameters.ContainsKey("Applicant Last Name"))
                    {
                        priorCert = certificateDataReader.GetCertificateRecord(
                            DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                        uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                        Logger.Trace($"Renew uUId: {uUId}");
                        renewRequest = _requestManager.GetRenewalRequest(productInfo, uUId, csr, san);
                        Logger.Trace($"Renewal Request JSON: {JsonConvert.SerializeObject(renewRequest)}");
                        var renewResponse = Task.Run(async () => await CscGlobalClient.SubmitRenewalAsync(renewRequest))
                            .Result;
                        Logger.Trace($"Renewal Response JSON: {JsonConvert.SerializeObject(renewResponse)}");
                        Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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
                    Logger.Trace($"Entering Reissue Enrollment");
                    //One click won't work for this implementation b/c we are missing enrollment params
                    if (productInfo.ProductParameters.ContainsKey("Applicant Last Name"))
                    {
                        priorCert = certificateDataReader.GetCertificateRecord(
                        DataConversion.HexToBytes(productInfo.ProductParameters["PriorCertSN"]));
                        uUId = priorCert.CARequestID.Substring(0, 36); //uUId is a GUID
                        Logger.Trace($"Reissue uUId: {uUId}");
                        reissueRequest = _requestManager.GetReissueRequest(productInfo, uUId, csr, san);
                        Logger.Trace($"Reissue JSON: {JsonConvert.SerializeObject(reissueRequest)}");
                        var reissueResponse = Task.Run(async () => await CscGlobalClient.SubmitReissueAsync(reissueRequest))
                            .Result;
                        Logger.Trace($"Reissue Response JSON: {JsonConvert.SerializeObject(reissueResponse)}");
                        Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return null;
        }

        private X509Certificate2 FindRootCertificate(X509Certificate2Collection certificates)
        {
            Logger.MethodEntry();
            foreach (X509Certificate2 certificate in certificates)
            {
                Logger.Trace("Looping through all the certs to find the root");

                if (IsRootCertificate(certificate, certificates))
                {
                    Logger.Trace("Found Root");
                    return certificate;
                }
            }
            Logger.MethodExit();
            // Return null if no root certificate is found
            return null;
        }

        private bool IsRootCertificate(X509Certificate2 certificate, X509Certificate2Collection certificates)
        {
            Logger.MethodEntry();
            // Check if the certificate is self-signed
            if (certificate?.Subject == certificate?.Issuer)
            {
                Logger.Trace("Subject is equal to issuer");
                // Check if there is no issuer in the collection with a matching subject
                foreach (X509Certificate2 issuerCertificate in certificates)
                {
                    Logger.Trace("Checking if there is no issuer in the collection with matching subject");
                    if (issuerCertificate.Subject == certificate?.Subject && !issuerCertificate.Equals(certificate))
                    {
                        Logger.Trace("Subject equal cert subject and issuer cert not equal to certificate");
                        Logger.MethodExit();
                        return false;
                    }
                }
                Logger.MethodExit();
                return true;
            }
            Logger.MethodExit();
            return false;
        }

        private List<(X509Certificate2 certificate, string type)> GetCertificateChain(string jobCertificate)
        {
            Logger.MethodEntry();
            // Decode the base64-encoded chain to get the bytes
            byte[] certificateChainBytes = Convert.FromBase64String(jobCertificate);
            Logger.Trace($"Cert Chain Bytes: {certificateChainBytes}");

            // Create a collection to hold the certificates
            X509Certificate2Collection certificateCollection = new X509Certificate2Collection();

            Logger.Trace($"Created certificate collection");

            // Load the certificates from the byte array
            certificateCollection.Import(certificateChainBytes);

            Logger.Trace($"Imported collection");

            // Identify the root certificate
            X509Certificate2 rootCertificate = FindRootCertificate(certificateCollection);

            Logger.Trace("Found Root Certificate");

            // Create a list to hold the ordered certificates
            List<(X509Certificate2 certificate, string certType)> orderedCertificates = new List<(X509Certificate2, string)>();

            Logger.Trace("Created a list to hold the ordered certificates");

            // Add the root certificate to the ordered list
            if (rootCertificate != null)
                orderedCertificates.Add((rootCertificate, "root"));

            Logger.Trace("Added Root To Collection");

            // Add intermediate certificates to the ordered list and mark them as intermediate
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                Logger.Trace("In loop to Add intermediate certificates to the ordered list and mark them as intermediate");
                // Exclude root certificate
                if (!certificate.Equals(rootCertificate))
                {
                    Logger.Trace("Excluded root certificate");
                    // Check if the certificate is not the leaf certificate
                    bool isLeaf = true;
                    foreach (X509Certificate2 potentialIssuer in certificateCollection)
                    {
                        Logger.Trace("Check if the certificate is not the leaf certificate");
                        if (certificate?.Subject == potentialIssuer?.Issuer && potentialIssuer != null && !potentialIssuer.Equals(certificate))
                        {
                            Logger.Trace("Leaf is false");
                            isLeaf = false;
                            break;
                        }
                    }

                    // If the certificate is not the leaf certificate, add it as an intermediate certificate
                    if (!isLeaf)
                    {
                        Logger.Trace("If the certificate is not the leaf certificate, add it as an intermediate certificate");
                        orderedCertificates.Add((certificate, "intermediate"));
                    }
                }
            }

            // Add leaf certificates to the ordered list
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                Logger.Trace("Check for add leaf certificates to the ordered list");
                if (!orderedCertificates.Exists(c => c.certificate != null && c.certificate.Equals(certificate)))
                {
                    Logger.Trace("Added leaf certificates to the ordered list");
                    orderedCertificates.Add((certificate, "leaf"));
                }
            }
            Logger.MethodExit();
            return orderedCertificates;
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            var keyfactorCaId = caRequestId.Substring(0, 36);
            Logger.Trace($"Keyfactor Ca Id: {keyfactorCaId}");
            var certificateResponse =
                Task.Run(async () => await CscGlobalClient.SubmitGetCertificateAsync(keyfactorCaId))
                    .Result;

            var certCollection = GetCertificateChain(certificateResponse.Certificate);
            Logger.Trace($"Single Cert JSON: {JsonConvert.SerializeObject(certificateResponse)}");
            var leafItem = certCollection.Select(cert => cert.type = "leaf").FirstOrDefault();
            
            Logger.Trace($"Single Cert Leaf: {leafItem}");
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            return new CAConnectorCertificate
            {
                CARequestID = keyfactorCaId,
                Certificate = leafItem,
                Status = _requestManager.MapReturnStatus(certificateResponse.Status),
                SubmissionDate = Convert.ToDateTime(certificateResponse.OrderDate)
            };
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            CscGlobalClient = new CscGlobalClient(configProvider);
            var templateSync = configProvider.CAConnectionData["TemplateSync"].ToString();
            if (templateSync.ToUpper() == "ON") EnableTemplateSync = true;
            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
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