using CRM.Client.Helpers;
using CRM.Shared;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyEmailInboxService : ProxyRestClientService<EmailInbox>, IEmailInboxService
    {
        public ProxyEmailInboxService(HttpClient http) : base(http, ConstHelper.EmailInboxPath)
        {
        }

        public async Task<List<EmailInbox>> GetListAsync()
        {
            return await _http.GetFromJsonAsync<List<EmailInbox>>($"{_pathService}/list") ?? new List<EmailInbox>();
        }

        public async Task<EmailInbox?> GetItemAsync(int id)
        {
            return await _http.GetFromJsonAsync<EmailInbox?>($"{_pathService}/{id}");
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var resp = await _http.DeleteAsync($"{_pathService}/{id}");
            return resp.IsSuccessStatusCode;
        }
    }
}
