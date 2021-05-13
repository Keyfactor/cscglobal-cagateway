using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RegistrationError : IRegistrationError
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("value")] public string Value { get; set; }

    }
}
