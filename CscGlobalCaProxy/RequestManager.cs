using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CAProxy.AnyGateway.Models;
using CSS.PKI;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class RequestManager
    {
        private readonly CscGlobalCaProxy _cscGlobalCaProxy;

        public RequestManager(CscGlobalCaProxy cscGlobalCaProxy)
        {
            _cscGlobalCaProxy = cscGlobalCaProxy;
        }

        public RegistrationRequest GetAdminContact(EnrollmentProductInfo productInfo)
        {
            return new RegistrationRequest
            {
                CertificateType = productInfo.ProductParameters["Certificate Type"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                Term = productInfo.ProductParameters["Term"],
                ServerSoftware = productInfo.ProductParameters["Server Software"],
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                CustomFields = GetCustomFields(productInfo),
                Csr = GetCsr(productInfo)
            };
        }

        private string GetCsr(EnrollmentProductInfo productInfo)
        {
            throw new NotImplementedException();
        }

        private List<CustomField> GetCustomFields(EnrollmentProductInfo productInfo)
        {
            return new List<CustomField>();
        }

        public DomainControlValidation GetDomainControlValidation(EnrollmentProductInfo productInfo)
        {
            return new DomainControlValidation
            {
                MethodType = productInfo.ProductParameters["Domain Control Validation Method"],
                EmailAddress = productInfo.ProductParameters["DCV Email (admin@yourdomain.com)"]
            };
        }

        public RegistrationRequest GetRegistrationRequest(EnrollmentProductInfo productInfo,string csr)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            Char[] delimiters = { ' '};

            return new RegistrationRequest()
            {
                Csr = encodedString,
                ServerSoftware = productInfo.ProductParameters["Server Software"].Split(delimiters)[0],
                CertificateType = productInfo.ProductParameters["Certificate Type"].Split(delimiters)[0],
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"],
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"])
            };
        }

        public Notifications GetNotifications(EnrollmentProductInfo productInfo)
        {
            return new Notifications
            {
                Enabled = true,
                AdditionalNotificationEmails = productInfo.ProductParameters["Notification Email(s) Comma Seperated"].Split(',').ToList()
            };
        }

        public RenewalRequest GetRenewalRequest(EnrollmentProductInfo productInfo)
        {
            return new RenewalRequest
            {
                Uuid = productInfo.ProductParameters["Uuid"],
                Csr= GetCsr(productInfo),
                Term = productInfo.ProductParameters["Term"],
                ServerSoftware = productInfo.ProductParameters["Server Software"],
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                CustomFields = GetCustomFields(productInfo),
                SubjectAlternativeNames = GetSubjectAlternativeNames(productInfo)
            };
        }

        private List<SubjectAlternativeName> GetSubjectAlternativeNames(EnrollmentProductInfo productInfo)
        {
            throw new NotImplementedException();
        }

        public ReissueRequest GetReissueRequestRequest(EnrollmentProductInfo productInfo, string uUId,string csr)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            Char[] delimiters = { ' ' };

            return new ReissueRequest
            {
                Uuid = uUId,
                Csr= encodedString,
                ServerSoftware = productInfo.ProductParameters["Server Software"].Split(delimiters)[0],
                CertificateType = productInfo.ProductParameters["Certificate Type"].Split(delimiters)[0],
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"],
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"])
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo)
        {
            return new EvCertificateDetails();
        }

        public int MapReturnStatus(string cscGlobalStatus)
        {
            PKIConstants.Microsoft.RequestDisposition returnStatus;

            switch (cscGlobalStatus)
            {
                case "ACTIVE":
                    returnStatus = PKIConstants.Microsoft.RequestDisposition.ISSUED;
                    break;
                case "Initial":
                case "Pending":
                    returnStatus = PKIConstants.Microsoft.RequestDisposition.PENDING;
                    break;
                case "REVOKED":
                    returnStatus = PKIConstants.Microsoft.RequestDisposition.REVOKED;
                    break;
                default:
                    returnStatus = PKIConstants.Microsoft.RequestDisposition.UNKNOWN;
                    break;
            }

            return Convert.ToInt32(returnStatus);
        }
    }
}
