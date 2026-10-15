using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Turns an error returned by CSC Global into a message that tells the user what to do next.
        /// </summary>
        private static string DescribeError(IRegistrationError error)
        {
            if (error == null) return null;

            if (string.Equals(error.Code, "OPEN_ORDER_INPROGRESS", StringComparison.OrdinalIgnoreCase))
                return "CSC Global already has an order in progress for this certificate" +
                       (string.IsNullOrEmpty(error.Value) ? "" : $" (order {error.Value})") +
                       ". No new request was submitted. Once that order is approved and issued, the certificate " +
                       "will appear after the next sync.";

            return error.Description;
        }

        public EnrollmentResult GetRenewResponse(RenewalResponse renewResponse)
        {
            if (renewResponse.RegistrationError != null)
                return new EnrollmentResult
                {
                    Status = 30, //failure
                    CARequestID = renewResponse?.Result?.Status?.Uuid,
                    StatusMessage = DescribeError(renewResponse.RegistrationError)
                };

            return new EnrollmentResult
            {
                Status = 13, //success
                CARequestID = renewResponse?.Result?.Status?.Uuid,
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
                    StatusMessage = DescribeError(registrationResponse.RegistrationError)
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
                    StatusMessage = DescribeError(reissueResponse.RegistrationError)
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
                SubjectAlternativeNames = IsMultipleNamesType(certificateType) ? GetSubjectAlternativeNames(productInfo, sans) : null,
                EvCertificateDetails = IsEvType(certificateType) ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private const string UnknownCertificateType = "-1";

        //CSC certificateType values. The current CSC names come first, the legacy names are kept so templates that
        //were already created in Keyfactor Command keep working. CSC names contain commas, which Active Directory
        //template names can't, so the lookup below ignores commas and hyphens (see NormalizeProductName).
        private static readonly Dictionary<string, string> CertificateTypeNames =
            new Dictionary<string, string>
            {
                //current CSC names
                {"CSC TrustedSecure OV", "0"},
                {"CSC TrustedSecure OV Wildcard", "1"},
                {"CSC TrustedSecure OV, Multiple Names", "2"},
                {"CSC TrustedSecure EV", "3"},
                {"CSC TrustedSecure DV", "4"},
                {"CSC TrustedSecure DV Wildcard", "5"},
                {"CSC TrustedSecure DV, Multiple Names", "6"},
                {"CSC TrustedSecure EV, Multiple Names", "7"},
                {"CSC TrustedSecure OV Wildcard, Multiple Names", "8"},
                {"CSC TrustedSecure DV Wildcard, Multiple Names", "9"},

                //legacy names
                {"CSC TrustedSecure Premium Certificate", "0"},
                {"CSC TrustedSecure Premium Wildcard Certificate", "1"},
                {"CSC TrustedSecure UC Certificate", "2"},
                {"CSC TrustedSecure EV Certificate", "3"},
                {"CSC TrustedSecure Domain Validated SSL", "4"},
                {"CSC TrustedSecure Domain Validated Wildcard SSL", "5"},
                {"CSC TrustedSecure Domain Validated UC Certificate", "6"},
                {"CSC Trusted Secure Domain Validated SSL", "4"},
                {"CSC Trusted Secure Domain Validated Wildcard SSL", "5"},
                {"CSC Trusted Secure Domain Validated UC Certificate", "6"}
            };

        //Certificate types that carry additional subject alternative names
        private static readonly HashSet<string> MultipleNamesTypes = new HashSet<string> {"2", "6", "7", "8", "9"};

        //Certificate types that need extended validation details
        private static readonly HashSet<string> EvTypes = new HashSet<string> {"3", "7"};

        private static bool IsMultipleNamesType(string certificateType)
        {
            return MultipleNamesTypes.Contains(certificateType);
        }

        private static bool IsEvType(string certificateType)
        {
            return EvTypes.Contains(certificateType);
        }

        private static readonly Dictionary<string, string> CertificateTypes =
            CertificateTypeNames.ToDictionary(entry => NormalizeProductName(entry.Key), entry => entry.Value);

        /// <summary>
        /// Lower cases a product name and treats commas, hyphens and runs of spaces as a single space, so
        /// "OV, Multiple Names", "OV - Multiple Names" and "OV-Multiple Names" all compare equal.
        /// </summary>
        private static string NormalizeProductName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var separated = name.Replace(',', ' ').Replace('-', ' ');
            return string.Join(" ", separated.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();
        }

        /// <summary>
        /// Converts a CSC certificate type name to the product name used for templates. Active Directory template
        /// names can't contain commas, so "CSC TrustedSecure OV, Multiple Names" becomes
        /// "CSC TrustedSecure OV - Multiple Names".
        /// </summary>
        public string GetTemplateProductName(string cscCertificateType)
        {
            if (string.IsNullOrWhiteSpace(cscCertificateType)) return cscCertificateType;

            return Regex.Replace(cscCertificateType.Trim(), @"\s*,\s*", " - ");
        }

        private string GetCertificateType(string productId)
        {
            var name = NormalizeProductName(productId);
            if (name.Length > 0 && CertificateTypes.TryGetValue(name, out var certificateType))
                return certificateType;

            return UnknownCertificateType;
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
                SubjectAlternativeNames = IsMultipleNamesType(certificateType) ? GetSubjectAlternativeNames(productInfo, sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = IsEvType(certificateType) ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private List<SubjectAlternativeName> GetSubjectAlternativeNames(EnrollmentProductInfo productInfo,
            Dictionary<string, string[]> sans)
        {
            var subjectNameList = new List<SubjectAlternativeName>();
            var methodType = productInfo.ProductParameters["Domain Control Validation Method"];

            if (sans == null || !sans.TryGetValue("dns", out var dnsNames) || dnsNames == null)
                return subjectNameList;

            //Only used for EMAIL validation, a CNAME validation does not need any addresses
            productInfo.ProductParameters.TryGetValue("Addtl Sans Comma Separated DVC Emails", out var sanEmails);
            var emailAddresses = (sanEmails ?? string.Empty).Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

            foreach (var v in dnsNames)
            {
                var domainName = v;
                var san = new SubjectAlternativeName();
                san.DomainName = domainName;
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
                SubjectAlternativeNames = IsMultipleNamesType(certificateType) ? GetSubjectAlternativeNames(productInfo, sans) : null,
                CustomFields = GetCustomFields(productInfo),
                EvCertificateDetails = IsEvType(certificateType) ? GetEvCertificateDetails(productInfo) : null
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo)
        {
            if (!productInfo.ProductParameters.TryGetValue("Organization Country", out var country) ||
                string.IsNullOrWhiteSpace(country))
                throw new InvalidOperationException(
                    "The 'Organization Country' enrollment parameter is required for extended validation (EV) certificates.");

            var evDetails = new EvCertificateDetails();
            evDetails.Country = country;
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