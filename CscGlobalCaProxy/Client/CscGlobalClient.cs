using System;
using System.Net;
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
    public sealed class CscGlobalClient : LoggingClientBase, ICscGlobalClient
    {
        public CscGlobalClient(ICAConnectorConfigProvider config)
        {
            if (config.CAConnectionData.ContainsKey(Constants.CscGlobalApiKey))
            {
                BaseUrl = new Uri(config.CAConnectionData[Constants.CscGlobalUrl].ToString());
                ApiKey = config.CAConnectionData[Constants.CscGlobalApiKey].ToString();
                Authorization = config.CAConnectionData[Constants.BearerToken].ToString();
                RestClient = ConfigureRestClient();
            }
        }

        private Uri BaseUrl { get; }
        private HttpClient RestClient { get; }
        private string ApiKey { get; }
        private string Authorization { get; }

        public async Task<RegistrationResponse> SubmitRegistrationAsync(
            RegistrationRequest registerRequest)
        {
            using (var resp = await RestClient.PostAsync("/dbs/api/v2/tls/registration", new StringContent(
                JsonConvert.SerializeObject(registerRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(registerRequest));
                var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                {
                    var errorResponse =
                        JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(),
                            settings);
                    var response = new RegistrationResponse();
                    response.RegistrationError = errorResponse;
                    response.Result = null;
                    return response;
                }

                var registrationResponse =
                    JsonConvert.DeserializeObject<RegistrationResponse>(await resp.Content.ReadAsStringAsync(),
                        settings);
                return registrationResponse;
            }
        }

        public async Task<RenewalResponse> SubmitRenewalAsync(
            RenewalRequest renewalRequest)
        {
            using (var resp = await RestClient.PostAsync("/tls/renewal", new StringContent(
                JsonConvert.SerializeObject(renewalRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(renewalRequest));

                var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                {
                    var errorResponse =
                        JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(),
                            settings);
                    var response = new RenewalResponse();
                    response.RegistrationError = errorResponse;
                    response.Result = null;
                    return response;
                }

                var renewalResponse =
                    JsonConvert.DeserializeObject<RenewalResponse>(await resp.Content.ReadAsStringAsync());
                return renewalResponse;
            }
        }

        public async Task<ReissueResponse> SubmitReissueAsync(
            ReissueRequest reissueRequest)
        {
            using (var resp = await RestClient.PostAsync("/dbs/api/v2/tls/reissue", new StringContent(
                JsonConvert.SerializeObject(reissueRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(reissueRequest));

                var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                {
                    var errorResponse =
                        JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(),
                            settings);
                    var response = new ReissueResponse();
                    response.RegistrationError = errorResponse;
                    response.Result = null;
                    return response;
                }

                var reissueResponse =
                    JsonConvert.DeserializeObject<ReissueResponse>(await resp.Content.ReadAsStringAsync());
                return reissueResponse;
            }
        }

        public async Task<CertificateResponse> SubmitGetCertificateAsync(string certificateId)
        {
            using (var resp = await RestClient.GetAsync($"/dbs/api/v2/tls/certificate/{certificateId}"))
            {
                resp.EnsureSuccessStatusCode();
                var getCertificateResponse =
                    JsonConvert.DeserializeObject<CertificateResponse>(await resp.Content.ReadAsStringAsync());
                return getCertificateResponse;
            }
        }

        public async Task<RevokeResponse> SubmitRevokeCertificateAsync(string uuId)
        {
            using (var resp = await RestClient.PutAsync($"/dbs/api/v2/tls/revoke/{uuId}", new StringContent("")))
            {
                var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                {
                    var errorResponse =
                        JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(),
                            settings);
                    var response = new RevokeResponse();
                    response.RegistrationError = errorResponse;
                    response.RevokeSuccess = null;
                    return response;
                }

                var getRevokeResponse =
                    JsonConvert.DeserializeObject<RevokeResponse>(await resp.Content.ReadAsStringAsync());
                return getRevokeResponse;
            }
        }

        public async Task<CertificateListResponse> SubmitCertificateListRequestAsync()
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            var resp = RestClient.GetAsync("/dbs/api/v2/tls/certificate?filter=status=in=(ACTIVE,REVOKED)").Result;

            if (!resp.IsSuccessStatusCode)
            {
                var responseMessage = resp.Content.ReadAsStringAsync().Result;
                Logger.Error(
                    $"Failed Request to Keyfactor. Retrying request. Status Code {resp.StatusCode} | Message: {responseMessage}");
            }

            var certificateListResponse =
                JsonConvert.DeserializeObject<CertificateListResponse>(await resp.Content.ReadAsStringAsync());
            return certificateListResponse;
        }

        private HttpClient ConfigureRestClient()
        {
            var clientHandler = new WebRequestHandler();
            var returnClient = new HttpClient(clientHandler, true) { BaseAddress = BaseUrl };
            returnClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            returnClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Authorization);
            returnClient.DefaultRequestHeaders.Add("apikey", ApiKey);
            return returnClient;
        }
    }
}