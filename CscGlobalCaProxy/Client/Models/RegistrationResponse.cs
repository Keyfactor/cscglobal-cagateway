using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RegistrationResponse : IRegistrationResponse
    {
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)] public Result Result { get; set; }
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public RegistrationError RegistrationError { get; set; }
    }
}
