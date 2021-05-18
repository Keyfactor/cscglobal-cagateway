using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RevokeResponse : IRevokeResponse
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public RevokeSuccessResponse RevokeSuccess { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public RegistrationError RegistrationError { get; set; }
    }
}
