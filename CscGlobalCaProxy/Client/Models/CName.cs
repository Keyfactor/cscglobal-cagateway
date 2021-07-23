using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class CName : ICName
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("value")] public string Value { get; set; }
    }
}
