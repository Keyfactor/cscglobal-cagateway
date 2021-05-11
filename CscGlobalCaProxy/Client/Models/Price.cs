using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class Price : IPrice
    {
        [JsonProperty("currency")] public string Currency { get; set; }
        [JsonProperty("total")] public decimal Total { get; set; }
    }
}
