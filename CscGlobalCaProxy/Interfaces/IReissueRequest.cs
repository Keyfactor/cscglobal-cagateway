using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IReissueRequest
    {
        string Uuid { get; set; }
        string Csr { get; set; }
        string ServerSoftware { get; set; }
        DomainControlValidation DomainControlValidation { get; set; }
        Notifications Notifications { get; set; }
        bool ShowPrice { get; set; }
        List<CustomField> CustomFields { get; set; }
        EvCertificateDetails EvCertificateDetails { get; set; }
    }
}