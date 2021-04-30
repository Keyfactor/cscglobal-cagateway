using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RegistrationResponse : IRegistrationResponse
    {
        [JsonProperty("result")] public Result Result { get; set; }
    }
}
