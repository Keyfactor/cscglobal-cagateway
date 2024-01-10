using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using CAProxy.AnyGateway.Models;
using CSS.PKI;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class RequestManager
    {
        private List<CustomField> GetCustomFields(EnrollmentProductInfo productInfo)
        {
            var customFieldList = new List<CustomField>();
            return customFieldList;
        }

        public EnrollmentResult GetRenewResponse(RenewalResponse renewResponse)
        {
            if (renewResponse.RegistrationError != null)
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    CARequestID = renewResponse.Result.Status.Uuid,
                    StatusMessage = renewResponse.RegistrationError.Description
                };

            return new EnrollmentResult
            {
                Status = 13, //success

                StatusMessage = $"Renewal Successfully Completed For {renewResponse.Result.CommonName}"
            };
        }


        public EnrollmentResult
            GetEnrollmentResult(
                IRegistrationResponse registrationResponse)
        {
            if (registrationResponse.RegistrationError != null)
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    StatusMessage = registrationResponse.RegistrationError.Description
                };

            return new EnrollmentResult
            {
                Status = 13, //success
                CARequestID = registrationResponse.Result.Status.Uuid,
                StatusMessage =
                    $"Order Successfully Created With Order Number {registrationResponse.Result.CommonName}"
            };
        }

        public int GetRevokeResult(IRevokeResponse revokeResponse)
        {
            if (revokeResponse.RegistrationError != null)
                return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.FAILED);

            return Convert.ToInt32(PKIConstants.Microsoft.RequestDisposition.REVOKED);
        }

        public EnrollmentResult GetReIssueResult(IReissueResponse reissueResponse)
        {
            if (reissueResponse.RegistrationError != null)
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    StatusMessage = reissueResponse.RegistrationError.Description
                };

            return new EnrollmentResult
            {
                Status = 13, //success
                CARequestID = reissueResponse.Result.Status.Uuid,
                StatusMessage = $"Reissue Successfully Completed For {reissueResponse.Result.CommonName}"
            };
        }

        public DomainControlValidation GetDomainControlValidation(string methodType, string[] emailAddress,
            string domainName)
        {
            foreach (var address in emailAddress)
            {
                var email = new MailAddress(address);
                if (domainName.Contains(email.Host.Split('.')[0]))
                    return new DomainControlValidation
                    {
                        MethodType = methodType,
                        EmailAddress = email.ToString()
                    };
            }

            return null;
        }

        public DomainControlValidation GetDomainControlValidation(string methodType, string emailAddress)
        {
            return new DomainControlValidation
            {
                MethodType = methodType,
                EmailAddress = emailAddress
            };
        }


        public static Func<string, string> Pemify = ss =>
        ss.Length <= 64 ? ss : ss.Substring(0, 64) + "\n" + Pemify(ss.Substring(64));

        public RegistrationRequest GetRegistrationRequest(EnrollmentProductInfo productInfo, string csr,
            Dictionary<string, string[]> sans)
        {

            var cert = "-----BEGIN CERTIFICATE REQUEST-----\r\n";
            cert = cert + Pemify(csr);
            cert = cert + "\r\n-----END CERTIFICATE REQUEST-----";


            var bytes = Encoding.UTF8.GetBytes(cert);
            var encodedString = Convert.ToBase64String(bytes);
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
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"],
                DomainControlValidation = GetDomainControlValidation(methodType, commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true, //User should not have to fill this out
                CustomFields = GetCustomFields(productInfo),
                SubjectAlternativeNames = certificateType == "2" ? GetSubjectAlternativeNames(productInfo, sans) : null,
                EvCertificateDetails = certificateType == "3" ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private string GetCertificateType(string productId)
        {
            switch (productId)
            {
                case "CSC TrustedSecure Premium Certificate":
                    return "0";
                case "CSC TrustedSecure EV Certificate":
                    return "3";
                case "CSC TrustedSecure UC Certificate":
                    return "2";
                case "CSC TrustedSecure Premium Wildcard Certificate":
                    return "1";
                case "CSC TrustedSecure Domain Validated SSL":
                    return "4";
                case "CSC TrustedSecure Domain Validated Wildcard SSL":
                    return "5";
                case "CSC TrustedSecure Domain Validated UC Certificate":
                    return "6";
            }

            return "-1";
        }

        public Notifications GetNotifications(EnrollmentProductInfo productInfo)
        {
            return new Notifications
            {
                Enabled = true,
                AdditionalNotificationEmails = productInfo.ProductParameters["Notification Email(s) Comma Separated"]
                    .Split(',').ToList()
            };
        }

        public RenewalRequest GetRenewalRequest(EnrollmentProductInfo productInfo, string uUId, string csr,
            Dictionary<string, string[]> sans)
        {
            var cert = "-----BEGIN CERTIFICATE REQUEST-----\r\n";
            cert = cert + Pemify(csr);
            cert = cert + "\r\n-----END CERTIFICATE REQUEST-----";

            var bytes = Encoding.UTF8.GetBytes(cert);
            var encodedString = Convert.ToBase64String(bytes);
            var commonNameValidationEmail = productInfo.ProductParameters["CN DCV Email (admin@yourdomain.com)"];
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
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"],
                DomainControlValidation = GetDomainControlValidation(methodType, commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true,
                SubjectAlternativeNames = certificateType == "2" ? GetSubjectAlternativeNames(productInfo, sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = certificateType == "3" ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private List<SubjectAlternativeName> GetSubjectAlternativeNames(EnrollmentProductInfo productInfo,
            Dictionary<string, string[]> sans)
        {
            var subjectNameList = new List<SubjectAlternativeName>();
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];

            foreach (var v in sans["dns"])
            {
                var domainName = v;
                var san = new SubjectAlternativeName();
                san.DomainName = domainName;
                var emailAddresses = productInfo.ProductParameters["Addtl Sans Comma Separated DVC Emails"].Split(',');
                if (methodType.ToUpper() == "EMAIL")
                    san.DomainControlValidation = GetDomainControlValidation(methodType, emailAddresses, domainName);
                else //it is a CNAME validation so no email is needed
                    san.DomainControlValidation = GetDomainControlValidation(methodType, "");

                subjectNameList.Add(san);
            }

            return subjectNameList;
        }

        public ReissueRequest GetReissueRequest(EnrollmentProductInfo productInfo, string uUId, string csr,
            Dictionary<string, string[]> sans)
        {
            var cert = "-----BEGIN CERTIFICATE REQUEST-----\r\n";
            cert = cert + Pemify(csr);
            cert = cert + "\r\n-----END CERTIFICATE REQUEST-----";

            var bytes = Encoding.UTF8.GetBytes(cert);
            var encodedString = Convert.ToBase64String(bytes);
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
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"],
                DomainControlValidation = GetDomainControlValidation(methodType, commonNameValidationEmail),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = true,
                SubjectAlternativeNames = certificateType == "2" ? GetSubjectAlternativeNames(productInfo, sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = certificateType == "3" ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo)
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