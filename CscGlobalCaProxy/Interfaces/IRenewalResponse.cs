using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IRenewalResponse
    {
        Result Result { get; set; }
        RegistrationError RegistrationError { get; set; }

    }
}