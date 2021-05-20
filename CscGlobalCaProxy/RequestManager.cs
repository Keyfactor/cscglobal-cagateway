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


        private List<CustomField> GetCustomFields(EnrollmentProductInfo productInfo) //todo add PO Number to UI and fill in using this make sure they have field setup
        {
            var poNumber=new CustomField();
            poNumber.Name = "PO Number";
            poNumber.Value = productInfo.ProductParameters["PO Number"];
            var customFieldList=new List<CustomField>();
            customFieldList.Add(poNumber);

            return customFieldList;
        }

        public DomainControlValidation GetDomainControlValidation(string methodType,string emailAddress)
        {
                return new DomainControlValidation
                {
                    MethodType = methodType,
                    EmailAddress = emailAddress
                };
        }

        public RegistrationRequest GetRegistrationRequest(EnrollmentProductInfo productInfo, string csr, Dictionary<string, string[]> sans)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};
            var commonNameValidationEmail = productInfo.ProductParameters["CN DCV Email (admin@yourdomain.com)"];
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];
            var certificateType = GetCertificateType(productInfo.ProductID);

            return new RegistrationRequest
            {
                Csr = encodedString,
                ServerSoftware = "-1", //Just default to other, user does not need to fill this in
                CertificateType = certificateType,
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(methodType,commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true,//User should not have to fill this out
                CustomFields = GetCustomFields(productInfo),
                SubjectAlternativeNames= certificateType == "2" ? GetSubjectAlternativeNames(productInfo,sans) : null,
                EvCertificateDetails = certificateType=="3"?GetEvCertificateDetails(productInfo):null
            };
        }

        private string GetCertificateType(string productID)
        {
            switch(productID)
            {
                case "CscGlobal-Premium":
                    return "0";
                case "CscGlobal-EV":
                    return "3";
                case "CscGlobal-UCC":
                    return "2";
                case "CscGlobal-Wildcard":
                    return "1";
            }
            return "-1";
        }

        public Notifications GetNotifications(EnrollmentProductInfo productInfo)
        {
            return new Notifications
            {
                Enabled = true,
                AdditionalNotificationEmails = productInfo.ProductParameters["Notification Email(s) Comma Seperated"]//todo fix spelling error in UI and  here
                    .Split(',').ToList()
            };
        }

        public RenewalRequest GetRenewalRequest(EnrollmentProductInfo productInfo, string uUId, string csr, Dictionary<string, string[]> sans)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};
            var commonNameValidationEmail= productInfo.ProductParameters["CN DCV Email (admin@yourdomain.com)"];
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];
            var certificateType = GetCertificateType(productInfo.ProductID);

            return new RenewalRequest
            {
                Uuid = uUId,
                Csr = encodedString,
                ServerSoftware = "-1",
                CertificateType = certificateType,
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(methodType, commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true,
                SubjectAlternativeNames = certificateType == "2" ? GetSubjectAlternativeNames(productInfo,sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = certificateType == "3" ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private List<SubjectAlternativeName> GetSubjectAlternativeNames(EnrollmentProductInfo productInfo,Dictionary<string, string[]> sans)
        {
            var subjectNameList=new List<SubjectAlternativeName>();
            string[] emailAddresses;
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];

            foreach (var k in sans.Keys)
            {
                var san = new SubjectAlternativeName();
                san.DomainName = sans[k][0];
                emailAddresses = productInfo.ProductParameters["Addtl Sans Comma Separated DVC Emails"].Split(',');
                if (methodType.ToUpper() == "EMAIL")
                {
                    foreach(var email in emailAddresses)
                    {
                        san.DomainControlValidation = GetDomainControlValidation(methodType, email);
                    }
                }
                else //it is a CNAME validation so no email is needed
                {
                    san.DomainControlValidation = GetDomainControlValidation(methodType, "");
                }
                               
                subjectNameList.Add(san);
            }

            return subjectNameList;
        }

        public ReissueRequest GetReissueRequest(EnrollmentProductInfo productInfo, string uUId, string csr, Dictionary<string, string[]> sans) 
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};
            var commonNameValidationEmail = productInfo.ProductParameters["CN DCV Email (admin@yourdomain.com)"];
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];
            var certificateType = GetCertificateType(productInfo.ProductID);

            return new ReissueRequest
            {
                Uuid = uUId,
                Csr = encodedString,
                ServerSoftware = "-1",
                CertificateType = GetCertificateType(productInfo.ProductID),
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(methodType,commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true,
                SubjectAlternativeNames = certificateType == "2" ? GetSubjectAlternativeNames(productInfo,sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = certificateType == "3" ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo) //todo fix this once I find out from Walt the standard requests.
        {
            var evDetails = new EvCertificateDetails();
            evDetails.Country = productInfo.ProductParameters["Organization Country"];
            return evDetails;
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