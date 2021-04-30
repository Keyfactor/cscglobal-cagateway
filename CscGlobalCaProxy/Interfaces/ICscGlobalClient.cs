using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface ICscGlobalClient
    {
        Task<IRegistrationResponse> SubmitRegistrationAsync(
            RegistrationRequest registerRequest);

        Task<IRenewalResponse> SubmitRenewalAsync(
            RenewalRequest renewalRequest);

        Task<IReissueResponse> SubmitReissueAsync(
            ReissueRequest reissueRequest);

        Task<ICertificateResponse> SubmitGetCertificateAsync(string certificateId);
        Task SubmitQueryTemplatesRequestAsync(BlockingCollection<ICertificateResponse> bc, CancellationToken ct);
    }
}