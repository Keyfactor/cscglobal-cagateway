using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class CertificateResponse : ICertificateResponse
    {
        [JsonProperty("uuid")] public string Uuid { get; set; }
        [JsonProperty("commonName")] public string CommonName { get; set; }
        [JsonProperty("additionalNames")] public List<string> AdditionalNames { get; set; }
        [JsonProperty("certificateType")] public string CertificateType { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("effectiveDate")] public string EffectiveDate { get; set; }
        [JsonProperty("expirationDate")] public string ExpirationDate { get; set; }
        [JsonProperty("businessUnit")] public string BusinessUnit { get; set; }
        [JsonProperty("orderedBy")] public string OrderedBy { get; set; }
        [JsonProperty("orderDate")] public string OrderDate { get; set; }
        [JsonProperty("serverSoftware")] public object ServerSoftware { get; set; }
        [JsonProperty("certificate")] public string Certificate { get; set; }
    }
}
