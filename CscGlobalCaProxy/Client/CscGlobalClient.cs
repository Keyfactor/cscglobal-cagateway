using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CAProxy.AnyGateway.Interfaces;
using CSS.Common.Logging;
using Keyfactor.AnyGateway.CscGlobal.Client.Models;
using Keyfactor.AnyGateway.CscGlobal.Exceptions;
using Keyfactor.AnyGateway.CscGlobal.Interfaces;
using Newtonsoft.Json;

namespace Keyfactor.AnyGateway.CscGlobal.Client
{
    public sealed class CscGlobalClient:LoggingClientBase, ICscGlobalClient
    {
        private Uri BaseUrl { get; }
        private HttpClient RestClient { get; }
        private int PageSize { get; } = 100;
        private string ApiKey { get; set; }
        private string Authorization { get; set; }


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

        private HttpClient ConfigureRestClient()
        {
            var clientHandler = new WebRequestHandler();
            var returnClient = new HttpClient(clientHandler, true) { BaseAddress = BaseUrl };
            returnClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            returnClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Authorization);
            returnClient.DefaultRequestHeaders.Add("apikey", ApiKey);
            return returnClient;
        }

        public async Task<RegistrationResponse> SubmitRegistrationAsync(
            RegistrationRequest registerRequest)
        {

                using (var resp = await RestClient.PostAsync("/dbs/api/v2/tls/registration", new StringContent(
                    JsonConvert.SerializeObject(registerRequest), Encoding.ASCII, "application/json")))
                {
                    Logger.Trace(JsonConvert.SerializeObject(registerRequest));
                    var settings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
                    if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                    {
                        var errorResponse =
                        JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(), settings);
                        RegistrationResponse response = new RegistrationResponse();
                        response.RegistrationError = errorResponse;
                        response.Result = null;
                        return response;
                    }
                    else
                    {
                        var registrationResponse =
                            JsonConvert.DeserializeObject<RegistrationResponse>(await resp.Content.ReadAsStringAsync(), settings);
                        return registrationResponse;
                    }


                }
        }

        public async Task<RenewalResponse> SubmitRenewalAsync(
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

        public async Task<ReissueResponse> SubmitReissueAsync(
            ReissueRequest reissueRequest)
        {
            using (var resp = await RestClient.PostAsync("/dbs/api/v2/tls/reissue", new StringContent(
                JsonConvert.SerializeObject(reissueRequest), Encoding.ASCII, "application/json")))
            {
                Logger.Trace(JsonConvert.SerializeObject(reissueRequest));

                var settings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
                if (resp.StatusCode == HttpStatusCode.BadRequest) //Csc Sends Errors back in 400 Json Response
                {
                    var errorResponse =
                    JsonConvert.DeserializeObject<RegistrationError>(await resp.Content.ReadAsStringAsync(), settings);
                    ReissueResponse response = new ReissueResponse();
                    response.RegistrationError = errorResponse;
                    response.Result = null;
                    return response;
                }
                else
                {
                    var reissueResponse =
                        JsonConvert.DeserializeObject<ReissueResponse>(await resp.Content.ReadAsStringAsync());
                    return reissueResponse;
                }
            }
        }

        public async Task<CertificateResponse> SubmitGetCertificateAsync(string certificateId)
        {
            using (var resp = await RestClient.GetAsync($"/tls/certificate/{certificateId}"))
            {
                resp.EnsureSuccessStatusCode();
                var getCertificateResponse =
                    JsonConvert.DeserializeObject<CertificateResponse>(await resp.Content.ReadAsStringAsync());
                return getCertificateResponse;
            }
        }

        public async Task<RevokeResponse> SubmitRevokeCertificateAsync(string uuId)
        {
            using (var resp = await RestClient.PutAsync($"/dbs/api/v2/tls/revoke/{uuId}",new StringContent("")))
            {
                var getRevokeResponse =
                    JsonConvert.DeserializeObject<RevokeResponse>(await resp.Content.ReadAsStringAsync());
                return getRevokeResponse;
            }
        }

        public async Task SubmitCertificateListRequestAsync(BlockingCollection<ICertificateResponse> bc, CancellationToken ct)
        {
            Logger.MethodEntry(ILogExtensions.MethodLogLevel.Debug);
            try
            {
                var itemsProcessed = 0;
                var pageCounter = 0;
                var isComplete = false;
                var retryCount = 0;
                do
                {
                    pageCounter++;
                    var batchItemsProcessed = 0;
                    using (var resp = await RestClient.GetAsync("/dbs/api/v2/tls/certificate?filter=status==Active"))
                    {
                        if (!resp.IsSuccessStatusCode)
                        {
                            var responseMessage = resp.Content.ReadAsStringAsync().Result;
                            Logger.Error(
                                $"Failed Request to Keyfactor. Retrying request. Status Code {resp.StatusCode} | Message: {responseMessage}");
                            retryCount++;
                            if (retryCount > 5)
                                throw new RetryCountExceededException(
                                    $"5 consecutive failures to {resp.RequestMessage.RequestUri}");

                            continue;
                        }

                        var stringResponse = await resp.Content.ReadAsStringAsync();

                        var batchResponse =
                            JsonConvert.DeserializeObject<CertificateListResponse>(stringResponse);

                        var batchCount = batchResponse.Results.Count;

                        Logger.Trace($"Processing {batchCount} items in batch");
                        do
                        {
                            var r = batchResponse.Results[batchItemsProcessed];
                            if (bc.TryAdd(r, 10, ct))
                            {
                                Logger.Trace($"Added Template ID {r.Uuid} to Queue for processing");
                                batchItemsProcessed++;
                                itemsProcessed++;
                                Logger.Trace($"Processed {batchItemsProcessed} of {batchCount}");
                                Logger.Trace($"Total Items Processed: {itemsProcessed}");
                            }
                            else
                            {
                                Logger.Trace($"Adding {r} blocked. Retry");
                            }
                        } while (batchItemsProcessed < batchCount); //batch loop

                    }

                    //assume that if we process less records than requested that we have reached the end of the certificate list
                    if (batchItemsProcessed < PageSize)
                        isComplete = true;
                } while (!isComplete); //page loop

                bc.CompleteAdding();
            }
            catch (OperationCanceledException cancelEx)
            {
                Logger.Warn($"Synchronize method was cancelled. Message: {cancelEx.Message}");
                bc.CompleteAdding();
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
                // ReSharper disable once PossibleIntendedRethrow
                throw cancelEx;
            }
            catch (RetryCountExceededException retryEx)
            {
                Logger.Error($"Retries Failed: {retryEx.Message}");
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"HttpRequest Failed: {ex.Message}");
                Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);
            }

            Logger.MethodExit(ILogExtensions.MethodLogLevel.Debug);

        }
    }
}
