using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client.Models
{
    public class Result : IResult
    {
        [JsonProperty("commonName")] public string CommonName { get; set; }
        [JsonProperty("status")] public Status Status { get; set; }
        [JsonProperty("certificateType")] public string CertificateType { get; set; }
        [JsonProperty("price")] public Price Price { get; set; }
        [JsonProperty("dcvDetails")] public List<DcvDetail> DcvDetails { get; set; }
    }
}