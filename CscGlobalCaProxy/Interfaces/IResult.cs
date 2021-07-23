using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IResult
    {
        string CommonName { get; set; }
        Status Status { get; set; }
        string CertificateType { get; set; }
        Price Price { get; set; }
        List<DcvDetail> DcvDetails { get; set; }
    }
}