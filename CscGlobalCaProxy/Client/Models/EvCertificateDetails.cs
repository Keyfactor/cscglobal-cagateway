using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class EvCertificateDetails : IEvCertificateDetails
    {
        [JsonProperty("country")] public string Country { get; set; }
        [JsonProperty("city")] public string City { get; set; }
        [JsonProperty("state")] public string State { get; set; }
        [JsonProperty("dateOfIncorporation")] public string DateOfIncorporation { get; set; }
        [JsonProperty("doingBusinessAs")] public string DoingBusinessAs { get; set; }
        [JsonProperty("businessCategory")] public string BusinessCategory { get; set; }
    }
}
