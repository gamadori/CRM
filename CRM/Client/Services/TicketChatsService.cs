using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{

    public class TicketChatsService : RestClientModelService<TicketChat, TicketChatViewModel, TicketChatFilterModel, int>, ITicketChatsService
    {

        public TicketChatsService(HttpClient http) : base(http, ConstHelper.TicketChatsPath)
        {

        }

        public async Task<bool> ChatRead(int idChat, TicketChatViewModel item)
        {
            try
            {
                var resp = await _http.PutAsJsonAsync<TicketChatViewModel>($"{_pathService}/MessageRead/{idChat}", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> TicketRead(int idTicket)
        {
            try
            {
                var resp = await _http.PutAsync($"{_pathService}/TicketRead/{idTicket}", null);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> HasNewMessage(int idTicket)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<bool>($"{_pathService}/HasNewMessage/{idTicket}");
                return resp;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<UnreadChatModel>> GetUnread()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<List<UnreadChatModel>>($"{_pathService}/Unread");
                return resp ?? new List<UnreadChatModel>();
            }
            catch
            {
                return new List<UnreadChatModel>();
            }
        }

        public async Task<ChatFileUploadResult?> UploadFile(int idTicket, IBrowserFile file)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
                content.Add(new StreamContent(stream), "file", file.Name);
                var resp = await _http.PostAsync($"{_pathService}/{idTicket}/upload", content);
                if (resp.IsSuccessStatusCode)
                    return await resp.Content.ReadFromJsonAsync<ChatFileUploadResult>();
                return null;
            }
            catch
            {
                return null;
            }
        }

    }
}
