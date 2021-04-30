using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RevokeResponse : IRevokeResponse
    {
        [JsonProperty("commonName")] public string CommonName { get; set; }
        [JsonProperty("certificateType")] public string CertificateType { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
    }
}
