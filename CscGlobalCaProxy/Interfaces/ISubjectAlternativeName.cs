using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface ISubjectAlternativeName
    {
        string DomainName { get; set; }
        DomainControlValidation DomainControlValidation { get; set; }
    }
}