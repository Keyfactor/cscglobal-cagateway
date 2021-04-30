using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RenewalRequest : IRenewalRequest
    {
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("csr")] public string Csr { get; set; }
        [JsonProperty("term")] public string Term { get; set; }
        [JsonProperty("serverSoftware")] public string ServerSoftware { get; set; }
        [JsonProperty("domainControlValidation")] public DomainControlValidation DomainControlValidation { get; set; }
        [JsonProperty("subjectAlternativeNames")] public List<SubjectAlternativeName> SubjectAlternativeNames { get; set; }
        [JsonProperty("notifications")] public Notifications Notifications { get; set; }
        [JsonProperty("showPrice")] public bool ShowPrice { get; set; }
        [JsonProperty("customFields")] public List<CustomField> CustomFields { get; set; }
    }
}
