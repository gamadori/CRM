using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyCommessaService : ProxyRestClientService<Commessa, CommessaDTO, int, CommessaFilter, int>, ICommessaService
    {
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public ProxyCommessaService(HttpClient http) : base(http, ConstHelper.CommessePath)
        {
        }

        public Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state)
            => PostAction<CommessaDTO>($"{_pathService}/{id}/state?state={state}");

        public Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId)
            => PostAction<List<CommessaDTO>>($"{_pathService}/from-orderrow/{orderRowId}");

        public Task<APIResponseMessage<List<CommessaDTO>>> StartInternalProductionAsync(InternalProductionRequestDTO req)
            => PostBody<InternalProductionRequestDTO, List<CommessaDTO>>($"{_pathService}/internal", req);

        public Task<APIResponseMessage<CommessaDTO>> OpenCommessaAsync(OpenCommessaRequestDTO req)
            => PostBody<OpenCommessaRequestDTO, CommessaDTO>($"{_pathService}/open-from-orderrow", req);

        public Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId)
            => PostAction<CommessaDTO>($"{_pathService}/orderrow/{orderRowId}/ready");

        // Data in formato invariante: sul querystring il formato locale del browser arriverebbe
        // ambiguo al binding del controller (03/04 giorno o mese?).
        public Task<APIResponseMessage<CommessaDTO>> RescheduleAsync(int id, DateTime delivery)
            => PostAction<CommessaDTO>($"{_pathService}/{id}/reschedule?delivery={delivery:yyyy-MM-dd}");

        public Task<APIResponseMessage<CommessaDTO>> RebuildPlanFromTemplateAsync(int id, DateTime delivery)
            => PostAction<CommessaDTO>($"{_pathService}/{id}/rebuild-plan?delivery={delivery:yyyy-MM-dd}");

        public async Task<List<CommessaDTO>> GetByOrderAsync(int orderId)
        {
            try
            {
                return await _http.GetFromJsonAsync<List<CommessaDTO>>($"{_pathService}/order/{orderId}") ?? new();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new();
            }
        }

        /// <summary>POST senza corpo: le azioni che stanno tutte nella rotta.</summary>
        private Task<APIResponseMessage<T>> PostAction<T>(string url)
            => Send<T>(() => _http.PostAsync(url, null));

        /// <summary>POST con corpo JSON.</summary>
        private Task<APIResponseMessage<T>> PostBody<TRequest, T>(string url, TRequest body)
            => Send<T>(() => _http.PostAsJsonAsync(url, body));

        private async Task<APIResponseMessage<T>> Send<T>(Func<Task<HttpResponseMessage>> call)
        {
            try
            {
                var resp = await call();
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<T>>(
                        await resp.Content.ReadAsStringAsync(), _json)
                        ?? new APIResponseMessage<T> { State = false, Message = "null" };
                }
                return new APIResponseMessage<T>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<T> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<T> { State = false, Message = ex.Message };
            }
        }
    }
}
