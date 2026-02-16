using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>
    /// Implementazione client del servizio TicketFeedback.
    /// </summary>
    /// ProxyRestClientService<LogEvent, int, LogEventFilterModel, object>
    public class ProxyTicketFeedbackService : ProxyRestClientService<TicketFeedback, TicketFeedbackResponse, int, TicketFeedbackFilterModel, object>, ITicketFeedbackService
    {
        
        private const string BaseUrl = "api/TicketFeedbacks";

        public ProxyTicketFeedbackService(HttpClient http) : base(http, BaseUrl)
        {

        }


        public async Task<List<TicketPendingFeedback>> GetPendingFeedbacksAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<TicketPendingFeedback>>($"{BaseUrl}/pending");
                return result ?? new List<TicketPendingFeedback>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetPendingFeedbacksAsync: {ex.Message}");
                return new List<TicketPendingFeedback>();
            }
        }

        public async Task<int> GetPendingFeedbacksCountAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<int>($"{BaseUrl}/pending/count");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetPendingFeedbacksCountAsync: {ex.Message}");
                return 0;
            }
        }

        public async Task<TicketFeedbackResponse> CreateFeedbackAsync(TicketFeedbackRequest request)
        {
            var response = await _http.PostAsJsonAsync(BaseUrl, request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TicketFeedbackResponse>();
            return result ?? throw new InvalidOperationException("Risposta vuota dal server");
        }

        public async Task<TicketFeedbackResponse?> GetFeedbackAsync(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<TicketFeedbackResponse>($"{BaseUrl}/{id}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<TicketFeedbackResponse?> GetFeedbackByTicketAsync(int ticketId)
        {
            try
            {
                return await _http.GetFromJsonAsync<TicketFeedbackResponse>($"{BaseUrl}/ticket/{ticketId}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> SkipFeedbackAsync(int ticketId)
        {
            try
            {
                var response = await _http.PostAsync($"{BaseUrl}/skip/{ticketId}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore SkipFeedbackAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<TicketFeedbackResponse>> GetAllFeedbacksAsync(bool unreadOnly = false)
        {
            try
            {
                var url = unreadOnly ? $"{BaseUrl}?unreadOnly=true" : BaseUrl;
                var result = await _http.GetFromJsonAsync<List<TicketFeedbackResponse>>(url);
                return result ?? new List<TicketFeedbackResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetAllFeedbacksAsync: {ex.Message}");
                return new List<TicketFeedbackResponse>();
            }
        }

        public async Task<bool> MarkAsReadAsync(int id)
        {
            try
            {
                var response = await _http.PutAsync($"{BaseUrl}/{id}/read", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore MarkAsReadAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<AverageFeedbackDTO> AverageRateAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<AverageFeedbackDTO>($"{BaseUrl}/average");
                return result ?? new AverageFeedbackDTO();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore AverageRateAsync: {ex.Message}");
                return new AverageFeedbackDTO();
            }
        }
    }
}
