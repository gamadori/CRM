using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyMachineParameterApiKeysService : IMachineParameterApiKeysService
    {
        private const string Path = "api/MachineParameterApiKeys";
        private readonly HttpClient _http;

        public ProxyMachineParameterApiKeysService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<MachineParameterApiKeyDTO>> GetListAsync()
        {
            return await _http.GetFromJsonAsync<List<MachineParameterApiKeyDTO>>(Path) ?? new();
        }

        public async Task<MachineParameterApiKeyCreateResponse?> CreateAsync(MachineParameterApiKeyCreateRequest request)
        {
            var response = await _http.PostAsJsonAsync(Path, request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MachineParameterApiKeyCreateResponse>();
        }

        public async Task<bool> RevokeAsync(int id)
        {
            var response = await _http.DeleteAsync($"{Path}/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
