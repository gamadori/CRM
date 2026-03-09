using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
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
    
    public class ProxyTicketsService: RestClientModelService<Ticket, TicketDTO, TicketFilter, int>, ITicketsService
    {
        
        public ProxyTicketsService(HttpClient http): base(http, ConstHelper.TicketPath)
        {
          
        }
        public async Task<bool> CloseTicket(int id, TicketClose item)
        {
            try
            {
                var resp = await _http.PutAsJsonAsync<TicketClose>($"{_pathService}/TicketClose/{id}", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ReOpenTicket(int id, Ticket item)
        {
            try
            {
                var resp = await _http.PutAsJsonAsync<Ticket>($"{_pathService}/TicketReOpen/{id}", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<PagingResponse<TicketDTO>> Search(TicketFilter args)
        {
            try
            {
                var resp = await this.Get(args, "search");

                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return new PagingResponse<TicketDTO>();
            }
        }

        public async Task<SemanticSearchResponse> SemanticSearch(SemanticSearchRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/semantic-search", request);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SemanticSearchResponse>() 
                        ?? new SemanticSearchResponse();
                }
                
                return new SemanticSearchResponse();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore semantic search: {ex.Message}");
                return new SemanticSearchResponse();
            }
        }

        public async Task<HashSet<string>?> LoadAssignedUsers(int IdTicket)
        {
            try
            {
                var response = await _http.GetFromJsonAsync<List<string>>($"{_pathService}/{IdTicket}/assigned-users");
                if (response != null)
                {
                    return new HashSet<string>(response);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti intervento: {ex.Message}");
                return null;
            }
        }

        public async Task<HttpResponseMessage> AssignUsers(int idTicket, AssignUsersRequest Users)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/{idTicket}/assign-users", Users);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assegnazione utenti al Ticket: {ex.Message}");
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

            }
        }
    }
}
