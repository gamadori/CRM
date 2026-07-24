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

        public Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId)
            => PostAction<CommessaDTO>($"{_pathService}/orderrow/{orderRowId}/ready");

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

        private async Task<APIResponseMessage<T>> PostAction<T>(string url)
        {
            try
            {
                var resp = await _http.PostAsync(url, null);
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
