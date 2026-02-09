using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CRM.Shared.DTOs;
using CRM.Shared.Services;

namespace CRM.Client.Services
{
    /// <summary>
    /// Implementazione client del servizio TicketFeedback.
    /// Effettua chiamate HTTP al controller.
    /// </summary>
    public class ProxyTicketFeedbackService : ITicketFeedbackService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "api/TicketFeedback";

        public ProxyTicketFeedbackService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<TicketPendingFeedback>> GetPendingFeedbacksAsync()
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<List<TicketPendingFeedback>>($"{BaseUrl}/pending");
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
                var result = await _httpClient.GetFromJsonAsync<int>($"{BaseUrl}/pending/count");
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
            var response = await _httpClient.PostAsJsonAsync(BaseUrl, request);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<TicketFeedbackResponse>();
            return result ?? throw new InvalidOperationException("Risposta vuota dal server");
        }

        public async Task<TicketFeedbackResponse?> GetFeedbackAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<TicketFeedbackResponse>($"{BaseUrl}/{id}");
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
                return await _httpClient.GetFromJsonAsync<TicketFeedbackResponse>($"{BaseUrl}/ticket/{ticketId}");
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
                var response = await _httpClient.PostAsync($"{BaseUrl}/skip/{ticketId}", null);
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
                var result = await _httpClient.GetFromJsonAsync<List<TicketFeedbackResponse>>(url);
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
                var response = await _httpClient.PutAsync($"{BaseUrl}/{id}/read", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore MarkAsReadAsync: {ex.Message}");
                return false;
            }
        }
    }
}
