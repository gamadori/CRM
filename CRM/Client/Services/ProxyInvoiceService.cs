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
    public class ProxyInvoiceService : ProxyRestClientService<Invoice, InvoiceDTO, int, InvoiceFilter, decimal>, IInvoiceService
    {
        public ProxyInvoiceService(HttpClient http) : base(http, ConstHelper.InvoicesPath)
        {
        }

        public Task<APIResponseMessage<InvoiceDTO>> CreateFromOrderAsync(int orderId)
            => PostAction($"{_pathService}/from-order/{orderId}");

        public Task<APIResponseMessage<InvoiceDTO>> SendAsync(int id)
            => PostAction($"{_pathService}/{id}/send");

        private async Task<APIResponseMessage<InvoiceDTO>> PostAction(string url)
        {
            try
            {
                var resp = await _http.PostAsync(url, null);
                if (resp.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<InvoiceDTO>>(
                        await resp.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions() { PropertyNameCaseInsensitive = true })
                        ?? new APIResponseMessage<InvoiceDTO> { State = false, Message = "null" };
                }
                return new APIResponseMessage<InvoiceDTO>
                {
                    State = false,
                    Code = resp.StatusCode,
                    Message = $"{resp.ReasonPhrase}\n\r{await resp.Content.ReadAsStringAsync()}"
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<InvoiceDTO> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new APIResponseMessage<InvoiceDTO> { State = false, Message = ex.Message };
            }
        }
    }
}
