using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyQuoteService : ProxyRestClientService<Quote, QuoteDTO, int, QuoteFilter, decimal>, IQuoteService
    {
        public ProxyQuoteService(HttpClient http) : base(http, ConstHelper.QuotesPath)
        {
        }

        public async Task<APIResponseMessage<QuoteDTO>> ChangeStateAsync(int id, QuoteStates state, bool updateDeal = true)
        {
            try
            {
                var resp = await _http.PostAsync($"{_pathService}/{id}/state?state={state}&updateDeal={updateDeal}", null);

                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<QuoteDTO>>(
                        await resp.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                        ?? new APIResponseMessage<QuoteDTO> { State = false, Message = "null" };
                }

                return new APIResponseMessage<QuoteDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<QuoteDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<QuoteDTO> { State = false, Message = ex.Message };
            }
        }

        public async Task<APIResponseMessage<QuoteDTO>> SendAsync(int id, QuoteSendRequest request)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_pathService}/{id}/send", request);

                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<QuoteDTO>>(
                        await resp.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                        ?? new APIResponseMessage<QuoteDTO> { State = false, Message = "null" };
                }

                return new APIResponseMessage<QuoteDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<QuoteDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<QuoteDTO> { State = false, Message = ex.Message };
            }
        }
    }
}
