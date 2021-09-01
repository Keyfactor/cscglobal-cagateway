using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IRevokeResponse
    {
        RevokeSuccessResponse RevokeSuccess { get; set; }
        RegistrationError RegistrationError { get; set; }
    }
}