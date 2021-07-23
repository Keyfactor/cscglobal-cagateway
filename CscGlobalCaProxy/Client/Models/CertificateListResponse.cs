using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class CertificateListResponse : ICertificateListResponse
    {
        [JsonProperty("meta")] public Meta Meta { get; set; }
        [JsonProperty("results")] public List<CertificateResponse> Results { get; set; }
    }

}
