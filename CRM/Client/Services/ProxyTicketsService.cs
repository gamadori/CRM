using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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

        public async Task<List<TicketScheduleItemDTO>> GetScheduleItemsAsync(TicketFilter args)
        {
            try
            {
                var query = new List<string>();

                if (args.DateFrom.HasValue)
                    query.Add($"dateFrom={Uri.EscapeDataString(args.DateFrom.Value.ToString("O"))}");

                if (args.DateTo.HasValue)
                    query.Add($"dateTo={Uri.EscapeDataString(args.DateTo.Value.ToString("O"))}");

                if (!string.IsNullOrWhiteSpace(args.IdUserAssigned))
                    query.Add($"idUserAssigned={Uri.EscapeDataString(args.IdUserAssigned)}");

                if (args.ViewNotAssigned)
                    query.Add("viewNotAssigned=true");

                var url = $"{_pathService}/schedule-items";
                if (query.Any())
                    url += "?" + string.Join("&", query);

                return await _http.GetFromJsonAsync<List<TicketScheduleItemDTO>>(url) ?? new List<TicketScheduleItemDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento pianificazione ticket: {ex.Message}");
                return new List<TicketScheduleItemDTO>();
            }
        }

        public async Task<bool> UpdateScheduleAsync(int idTicket, TicketScheduleUpdateRequest request)
        {
            try
            {
                var response = await _http.PutAsJsonAsync($"{_pathService}/{idTicket}/schedule", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore aggiornamento pianificazione ticket: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartProcessingAsync(int idTicket)
        {
            try
            {
                var response = await _http.PutAsync($"{_pathService}/{idTicket}/start-processing", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore avvio lavorazione ticket: {ex.Message}");
                return false;
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

        public async Task<TicketSummaryProposalResponse> ProposeSummary(int idTicket, TicketSummaryProposalRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/{idTicket}/summary/propose", request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<TicketSummaryProposalResponse>()
                        ?? new TicketSummaryProposalResponse { IdTicket = idTicket };
                }

                return new TicketSummaryProposalResponse
                {
                    IdTicket = idTicket,
                    Warning = $"Errore dal server ({(int)response.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore proposta riepilogo ticket: {ex.Message}");
                return new TicketSummaryProposalResponse
                {
                    IdTicket = idTicket,
                    Warning = ex.Message
                };
            }
        }

        public async Task<TicketDTO?> UpdateSummary(int idTicket, UpdateTicketSummaryRequest request)
        {
            try
            {
                var response = await _http.PutAsJsonAsync($"{_pathService}/{idTicket}/summary", request);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<TicketDTO>();

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio riepilogo ticket: {ex.Message}");
                return null;
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

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ClaimAsync(int idTicket)
        {
            try
            {
                var response = await _http.PostAsync($"{_pathService}/{idTicket}/claim", null);
                var payload = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<CRM.Client.Models.APIResponseMessage<TicketDTO>>(
                    payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result != null)
                    return result;

                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Code = response.StatusCode,
                    Message = response.IsSuccessStatusCode ? "Risposta vuota dal server" : payload
                };
            }
            catch (Exception ex)
            {
                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> BlockAsync(int idTicket, TicketBlockRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/{idTicket}/block", request);
                return await ReadTicketResponseAsync(response, "Blocco non riuscito");
            }
            catch (Exception ex)
            {
                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> UnblockAsync(int idTicket, TicketUnblockRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/{idTicket}/unblock", request);
                return await ReadTicketResponseAsync(response, "Sblocco non riuscito");
            }
            catch (Exception ex)
            {
                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> AcceptAiRoutingAsync(int idTicket)
        {
            try
            {
                var response = await _http.PostAsync($"{_pathService}/{idTicket}/ai-routing/accept", null);
                return await ReadTicketResponseAsync(response, "Assegnazione non riuscita");
            }
            catch (Exception ex)
            {
                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Message = ex.Message
                };
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> DismissAiRoutingAsync(int idTicket)
        {
            try
            {
                var response = await _http.PostAsync($"{_pathService}/{idTicket}/ai-routing/dismiss", null);
                return await ReadTicketResponseAsync(response, "Operazione non riuscita");
            }
            catch (Exception ex)
            {
                return new CRM.Client.Models.APIResponseMessage<TicketDTO>
                {
                    State = false,
                    Message = ex.Message
                };
            }
        }

        private static async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ReadTicketResponseAsync(HttpResponseMessage response, string fallback)
        {
            var payload = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<CRM.Client.Models.APIResponseMessage<TicketDTO>>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result != null)
                return result;

            return new CRM.Client.Models.APIResponseMessage<TicketDTO>
            {
                State = false,
                Code = response.StatusCode,
                Message = string.IsNullOrWhiteSpace(payload) ? fallback : payload
            };
        }
    }
}
