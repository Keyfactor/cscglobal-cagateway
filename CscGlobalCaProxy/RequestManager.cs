using System;
using System.Collections.Generic;
using System.Linq;
using CAProxy.AnyGateway.Models;
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
            throw new NotImplementedException();
        }

        public DomainControlValidation GetDomainControlValidation(EnrollmentProductInfo productInfo)
        {
            return new DomainControlValidation
            {
                MethodType = productInfo.ProductParameters["Method Type"],
                EmailAddress = productInfo.ProductParameters["Email Address"]
            };
        }

        public Notifications GetNotifications(EnrollmentProductInfo productInfo)
        {
            return new Notifications
            {
                Enabled = Convert.ToBoolean(productInfo.ProductParameters["Enabled"]),
                AdditionalNotificationEmails = productInfo.ProductParameters["Comma Separated Emails"].Split(',').ToList()
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

        public ReissueRequest GetReissueRequestRequest(EnrollmentProductInfo productInfo)
        {
            return new ReissueRequest
            {
                Uuid = productInfo.ProductParameters["Uuid"],
                Csr = GetCsr(productInfo),
                ServerSoftware = productInfo.ProductParameters["Server Software"],
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = GetEvCertificateDetails(productInfo)
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo)
        {
            throw new NotImplementedException();
        }
    }
}
