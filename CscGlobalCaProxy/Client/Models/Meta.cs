using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class Meta : IMeta
    {
        [JsonProperty("numResults")] public int NumResults { get; set; }
    }
}
