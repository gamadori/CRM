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

        public async Task<AssistantChatResponse> AssistantChat(AssistantChatRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/assistant-chat", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AssistantChatResponse>()
                        ?? new AssistantChatResponse { Success = false, Message = "Risposta vuota dal server" };
                }

                return new AssistantChatResponse
                {
                    Success = false,
                    Message = $"Errore dal server ({(int)response.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assistant chat: {ex.Message}");
                return new AssistantChatResponse { Success = false, Message = ex.Message };
            }
        }

        public async Task AssistantChatStream(
            AssistantChatRequest request,
            Action<List<TicketSimilarityResult>> onTickets,
            Action<string> onChunk,
            Action<string> onError,
            Action<int>? onLogId = null)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_pathService}/assistant-chat-stream")
                {
                    Content = JsonContent.Create(request)
                };
                // Abilita lo streaming della risposta nel browser (Blazor WASM)
                req.SetBrowserResponseStreamingEnabled(true);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    onError(string.IsNullOrWhiteSpace(err) ? $"Errore dal server ({(int)resp.StatusCode})" : err);
                    return;
                }

                // Id del log di questa risposta (per il feedback)
                if (onLogId != null && resp.Headers.TryGetValues("X-Assistant-Log-Id", out var logVals)
                    && int.TryParse(logVals.FirstOrDefault(), out var logId) && logId > 0)
                {
                    onLogId(logId);
                }

                // Ticket di riferimento dall'header (Base64/UTF-8 JSON)
                if (resp.Headers.TryGetValues("X-Referenced-Tickets", out var vals))
                {
                    var b64 = vals.FirstOrDefault();
                    if (!string.IsNullOrEmpty(b64))
                    {
                        try
                        {
                            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                            var tickets = JsonSerializer.Deserialize<List<TicketSimilarityResult>>(
                                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (tickets != null)
                                onTickets(tickets);
                        }
                        catch { /* header malformato: ignora */ }
                    }
                }

                using var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var buffer = new char[256];
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    onChunk(new string(buffer, 0, read));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assistant chat stream: {ex.Message}");
                onError(ex.Message);
            }
        }

        public async Task<bool> SendAssistantFeedback(AssistantFeedbackRequest request)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_pathService}/assistant-feedback", request);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore invio feedback assistente: {ex.Message}");
                return false;
            }
        }

        public async Task<List<AssistantChatLogDTO>> GetAssistantLogs(AssistantChatLogFilter filter)
        {
            var query = new List<string>();
            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                    query.Add($"Search={Uri.EscapeDataString(filter.Search)}");
                if (filter.Vote.HasValue)
                    query.Add($"Vote={filter.Vote.Value}");
            }

            var url = query.Count > 0
                ? $"{_pathService}/assistant-logs?{string.Join("&", query)}"
                : $"{_pathService}/assistant-logs";

            try
            {
                return await _http.GetFromJsonAsync<List<AssistantChatLogDTO>>(url) ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetAssistantLogs: {ex.Message}");
                return new();
            }
        }

        public async Task<AssistantChatResponse> DataAssistantAsk(AssistantChatRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("api/CrmAssistant/ask", request);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<AssistantChatResponse>()
                        ?? new AssistantChatResponse { Success = false, Message = "Risposta vuota dal server" };
                }

                return new AssistantChatResponse
                {
                    Success = false,
                    Message = $"Errore dal server ({(int)response.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore data assistant: {ex.Message}");
                return new AssistantChatResponse { Success = false, Message = ex.Message };
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
    }
}
