using CRM.Client.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

    }
}
