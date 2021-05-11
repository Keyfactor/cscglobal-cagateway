using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class RegistrationResponse : IRegistrationResponse
    {
        [JsonProperty("result",Required = Required.AllowNull)] public Result Result { get; set; }
        [JsonProperty(Required = Required.AllowNull)] public RegistrationError Error { get; set; }
    }
}
