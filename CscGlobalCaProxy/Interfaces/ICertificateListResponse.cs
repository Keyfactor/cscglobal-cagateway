using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface ICertificateListResponse
    {
        Meta Meta { get; set; }
        List<CertificateResponse> Results { get; set; }
    }
}