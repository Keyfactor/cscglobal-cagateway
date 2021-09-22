using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IDcvDetail
    {
        string DomainName { get; set; }
        string ActionNeeded { get; set; }
        string Email { get; set; }
        CName CName { get; set; }
    }
}