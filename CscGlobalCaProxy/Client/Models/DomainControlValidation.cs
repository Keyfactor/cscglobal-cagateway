using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class DomainControlValidation : IDomainControlValidation
    {
        [JsonProperty("methodType")] public string MethodType { get; set; }
        [JsonProperty("emailAddress")] public string EmailAddress { get; set; }
    }
}
