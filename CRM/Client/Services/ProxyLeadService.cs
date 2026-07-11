using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyLeadService : ProxyRestClientService<Lead, LeadDTO, int, LeadFilter, decimal>, ILeadService
    {
        private readonly HttpClient _httpClient;

        public ProxyLeadService(HttpClient http) : base(http, ConstHelper.LeadsPath)
        {
            _httpClient = http;
        }

        public async Task<APIResponseMessage<DealDTO>> ConvertAsync(int id, ConvertLeadRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{ConstHelper.LeadsPath}/{id}/convert", request);
            if (!response.IsSuccessStatusCode)
            {
                return new APIResponseMessage<DealDTO>
                {
                    State = false,
                    Code = response.StatusCode,
                    Message = await response.Content.ReadAsStringAsync()
                };
            }

            return JsonSerializer.Deserialize<APIResponseMessage<DealDTO>>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new APIResponseMessage<DealDTO> { State = false };
        }
    }
}
