using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CAProxy.AnyGateway.Interfaces;
using CSS.Common.Logging;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client
{
    public class CscGlobalClient:LoggingClientBase
    {
        private Uri BaseUrl { get; }
        private HttpClient RestClient { get; }
        private int PageSize { get; } = 100;

        public CscGlobalClient(ICAConnectorConfigProvider config)
        {
            if (config.CAConnectionData.ContainsKey(Constants.CscGlobalApiKey))
            {
                BaseUrl = new Uri(config.CAConnectionData[Constants.CscGlobalUrl].ToString());
                RestClient = ConfigureRestClient();
            }
        }

        private HttpClient ConfigureRestClient()
        {
            var clientHandler = new WebRequestHandler();
            var returnClient = new HttpClient(clientHandler, true) { BaseAddress = BaseUrl };
            returnClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return returnClient;
        }

        public async Task<IRegistrationResponse> SubmitRegistrationAsync(
            RegistrationRequest registerRequest)
        {
            using (var resp = await RestClient.PostAsync("/tls/registration", new StringContent(
                JsonConvert.SerializeObject(registerRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(registerRequest));
                resp.EnsureSuccessStatusCode();
                var registrationResponse =
                    JsonConvert.DeserializeObject<RegistrationResponse>(await resp.Content.ReadAsStringAsync());
                return registrationResponse;
            }
        }

        public async Task<IRenewalResponse> SubmitRenewalAsync(
            RenewalRequest renewalRequest)
        {
            using (var resp = await RestClient.PostAsync("/tls/renewal", new StringContent(
                JsonConvert.SerializeObject(renewalRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(renewalRequest));
                resp.EnsureSuccessStatusCode();
                var renewalResponse =
                    JsonConvert.DeserializeObject<RenewalResponse>(await resp.Content.ReadAsStringAsync());
                return renewalResponse;
            }
        }

        public async Task<IReissueResponse> SubmitReissueAsync(
            ReissueRequest reissueRequest)
        {
            using (var resp = await RestClient.PostAsync("/tls/reissue", new StringContent(
                JsonConvert.SerializeObject(reissueRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(reissueRequest));
                resp.EnsureSuccessStatusCode();
                var reissueResponse =
                    JsonConvert.DeserializeObject<ReissueResponse>(await resp.Content.ReadAsStringAsync());
                return reissueResponse;
            }
        }

        public async Task<ICertificateResponse> SubmitGetCertificateAsync(string certificateId)
        {
            using (var resp = await RestClient.GetAsync($"/tls/certificate/{certificateId}"))
            {
                resp.EnsureSuccessStatusCode();
                var getCertificateResponse =
                    JsonConvert.DeserializeObject<CertificateResponse>(await resp.Content.ReadAsStringAsync());
                return getCertificateResponse;
            }
        }

    }
}
