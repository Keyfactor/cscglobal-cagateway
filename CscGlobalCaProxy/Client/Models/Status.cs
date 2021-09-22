using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class Status : IStatus
    {
        [JsonProperty("code")] public string Code { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("additionalInformation")] public string AdditionalInformation { get; set; }
        [JsonProperty("uuid")] public string Uuid { get; set; }
    }
}
