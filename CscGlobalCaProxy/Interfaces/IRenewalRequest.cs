using System.Collections.Generic;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IRenewalRequest
    {
        string Uuid { get; set; }
        string Csr { get; set; }
        string Term { get; set; }
        string ServerSoftware { get; set; }
        DomainControlValidation DomainControlValidation { get; set; }
        List<SubjectAlternativeName> SubjectAlternativeNames { get; set; }
        Notifications Notifications { get; set; }
        bool ShowPrice { get; set; }
        List<CustomField> CustomFields { get; set; }
    }
}