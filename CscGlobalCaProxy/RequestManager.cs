﻿using System;
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


        private List<CustomField> GetCustomFields(EnrollmentProductInfo productInfo)
        {
            var poNumber=new CustomField();
            poNumber.Name = "PO Number";
            poNumber.Value = productInfo.ProductParameters["PO Number"];
            var customFieldList=new List<CustomField>();
            customFieldList.Add(poNumber);

            return customFieldList;
        }

        public DomainControlValidation GetDomainControlValidation(EnrollmentProductInfo productInfo)
        {
            return new DomainControlValidation
            {
                MethodType = productInfo.ProductParameters["Domain Control Validation Method"],
                EmailAddress = productInfo.ProductParameters["DCV Email (admin@yourdomain.com)"]
            };
        }

        public RegistrationRequest GetRegistrationRequest(EnrollmentProductInfo productInfo, string csr)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};

            return new RegistrationRequest
            {
                Csr = encodedString,
                ServerSoftware = productInfo.ProductParameters["Server Software"].Split(delimiters)[0],
                CertificateType = productInfo.ProductParameters["Certificate Type"].Split(delimiters)[0],
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                CustomFields = GetCustomFields(productInfo)
            };
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

        public RenewalRequest GetRenewalRequest(EnrollmentProductInfo productInfo, string uUId, string csr)
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};

            return new RenewalRequest
            {
                Uuid = uUId,
                Csr = encodedString,
                ServerSoftware = productInfo.ProductParameters["Server Software"].Split(delimiters)[0],
                CertificateType = productInfo.ProductParameters["Certificate Type"].Split(delimiters)[0],
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                SubjectAlternativeNames = GetSubjectAlternativeNames(productInfo),
                CustomFields = GetCustomFields(productInfo)
            };
        }

        private List<SubjectAlternativeName> GetSubjectAlternativeNames(EnrollmentProductInfo productInfo) //todo fix this to return Sans
        {
            return null;
        }

        public ReissueRequest GetReissueRequest(EnrollmentProductInfo productInfo, string uUId, string csr) 
        {
            var bytes = Encoding.UTF8.GetBytes(csr);
            var encodedString = Convert.ToBase64String(bytes);
            char[] delimiters = {' '};

            return new ReissueRequest
            {
                Uuid = uUId,
                Csr = encodedString,
                ServerSoftware = productInfo.ProductParameters["Server Software"].Split(delimiters)[0],
                CertificateType = productInfo.ProductParameters["Certificate Type"].Split(delimiters)[0],
                Term = productInfo.ProductParameters["Term"],
                ApplicantFirstName = productInfo.ProductParameters["Applicant First Name"],
                ApplicantLastName = productInfo.ProductParameters["Applicant Last Name"],
                ApplicantEmailAddress = productInfo.ProductParameters["Applicant Email Address"],
                ApplicantPhoneNumber = productInfo.ProductParameters["Applicant Phone (+nn.nnnnnnnn)"], //todo find out why only foreign numbers supported
                DomainControlValidation = GetDomainControlValidation(productInfo),
                Notifications = GetNotifications(productInfo),
                OrganizationContact = productInfo.ProductParameters["Organization Contact"],
                BusinessUnit = productInfo.ProductParameters["Business Unit"],
                ShowPrice = Convert.ToBoolean(productInfo.ProductParameters["Show Price"]),
                CustomFields = GetCustomFields(productInfo)
            };
        }

        private EvCertificateDetails GetEvCertificateDetails(EnrollmentProductInfo productInfo) //todo fix this once I find out from Walt the standard requests.
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