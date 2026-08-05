using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyApiKeysService : IApiKeysService
    {
        private const string Path = "api/ApiKeys";

        private readonly HttpClient _http;

        public ProxyApiKeysService(HttpClient http) => _http = http;

        public async Task<List<ApiKeyDTO>> GetAsync(ApiKeyScope? scope = null)
        {
            try
            {
                var url = scope == null ? Path : $"{Path}?scope={scope}";
                return await _http.GetFromJsonAsync<List<ApiKeyDTO>>(url) ?? new List<ApiKeyDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<ApiKeyDTO>();
            }
        }

        /// <summary>
        /// Restituisce anche il motivo del rifiuto: il server valida l'intestatario per ambito
        /// ("serve un'azienda", "serve una persona") e quel messaggio va mostrato, non inghiottito.
        /// </summary>
        public async Task<(ApiKeyCreateResponse? Response, string? Error)> CreateAsync(ApiKeyCreateRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync(Path, request);
                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<ApiKeyCreateResponse>(), null);

                var body = await response.Content.ReadAsStringAsync();
                return (null, ExtractMessage(body) ?? $"Creazione non riuscita ({(int)response.StatusCode}).");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<bool> RevokeAsync(int id)
        {
            try
            {
                var response = await _http.DeleteAsync($"{Path}/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        private static string? ExtractMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
