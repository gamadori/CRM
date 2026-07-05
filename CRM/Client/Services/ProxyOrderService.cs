using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyOrderService : ProxyRestClientService<Order, OrderDTO, int, OrderFilter, decimal>, IOrderService
    {
        public ProxyOrderService(HttpClient http) : base(http, ConstHelper.OrdersPath)
        {
        }

        public Task<APIResponseMessage<OrderDTO>> CreateFromQuoteAsync(int quoteId)
            => PostAction($"{_pathService}/from-quote/{quoteId}");

        public Task<APIResponseMessage<OrderDTO>> ChangeStateAsync(int id, OrderStates state)
            => PostAction($"{_pathService}/{id}/state?state={state}");

        private async Task<APIResponseMessage<OrderDTO>> PostAction(string url)
        {
            try
            {
                var resp = await _http.PostAsync(url, null);
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<OrderDTO>>(
                        await resp.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                        ?? new APIResponseMessage<OrderDTO> { State = false, Message = "null" };
                }
                return new APIResponseMessage<OrderDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<OrderDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<OrderDTO> { State = false, Message = ex.Message };
            }
        }
    }
}
