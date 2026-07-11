using CRM.Client.Helpers;
using CRM.Shared;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxySmtpSettingsService : ProxyRestClientService<SmtpSetting>, ISmtpSettingsService
    {
        public ProxySmtpSettingsService(HttpClient http) : base(http, ConstHelper.SmtpSettingsPath)
        {
        }

        public async Task<List<SmtpSetting>> GetListAsync()
        {
            return await _http.GetFromJsonAsync<List<SmtpSetting>>($"{_pathService}/list") ?? new List<SmtpSetting>();
        }

        public async Task<SmtpSetting?> GetItemAsync(int id)
        {
            return await _http.GetFromJsonAsync<SmtpSetting?>($"{_pathService}/{id}");
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{_pathService}/{id}");
            return resp.IsSuccessStatusCode;
        }

        public async Task<bool> ReorderAsync(List<int> orderedIds)
        {
            var resp = await _http.PostAsJsonAsync($"{_pathService}/reorder", orderedIds);
            return resp.IsSuccessStatusCode;
        }
    }
}
