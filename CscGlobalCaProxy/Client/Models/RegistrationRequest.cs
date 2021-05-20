using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RegistrationRequest : IRegistrationRequest
    {
        [JsonProperty("csr")] public string Csr { get; set; }
        [JsonProperty("certificateType")] public string CertificateType { get; set; }
        [JsonProperty("businessUnit")] public string BusinessUnit { get; set; }
        [JsonProperty("term")] public string Term { get; set; }
        [JsonProperty("serverSoftware")] public string ServerSoftware { get; set; }
        [JsonProperty("organizationContact")] public string OrganizationContact { get; set; }
        [JsonProperty("domainControlValidation")] public DomainControlValidation DomainControlValidation { get; set; }
        [JsonProperty("notifications")] public Notifications Notifications { get; set; }
        [JsonProperty("showPrice")] public bool ShowPrice { get; set; }
        [JsonProperty("customFields")] public List<CustomField> CustomFields { get; set; }
        [JsonProperty("applicantFirstName")] public string ApplicantFirstName { get; set; }
        [JsonProperty("applicantLastName")] public string ApplicantLastName { get; set; }
        [JsonProperty("applicantEmailAddress")] public string ApplicantEmailAddress { get; set; }
        [JsonProperty("applicantPhoneNumber")] public string ApplicantPhoneNumber { get; set; }
        [JsonProperty("evCertificateDetails",NullValueHandling=NullValueHandling.Ignore)] public EvCertificateDetails EvCertificateDetails { get; set; }
        [JsonProperty("subjectAlternativeNames", NullValueHandling = NullValueHandling.Ignore)] public List<SubjectAlternativeName> SubjectAlternativeNames { get; set; }
    }
}
