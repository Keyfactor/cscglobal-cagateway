using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface ICscGlobalClient
    {
        Task<RegistrationResponse> SubmitRegistrationAsync(
            RegistrationRequest registerRequest);

        Task<RenewalResponse> SubmitRenewalAsync(
            RenewalRequest renewalRequest);

        Task<ReissueResponse> SubmitReissueAsync(
            ReissueRequest reissueRequest);

        Task<CertificateResponse> SubmitGetCertificateAsync(string certificateId);

        Task SubmitCertificateListRequestAsync(BlockingCollection<ICertificateResponse> bc, CancellationToken ct);

        Task<RevokeResponse> SubmitRevokeCertificateAsync(string uuId);
    }
}