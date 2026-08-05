using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyInitiativeService : ProxyRestClientService<Initiative, InitiativeDTO, int, InitiativeFilter, decimal>, IInitiativeService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public ProxyInitiativeService(HttpClient http) : base(http, ConstHelper.InitiativesPath)
        {
            _httpClient = http;
        }

        public async Task<InitiativeSummaryDTO?> GetReportAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<InitiativeSummaryDTO?>($"{ConstHelper.InitiativesPath}/{id}/report");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<List<InitiativeLeadTriageDTO>> GetLeadTriageAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<InitiativeLeadTriageDTO>>(
                    $"{ConstHelper.InitiativesPath}/{id}/leads/triage") ?? new List<InitiativeLeadTriageDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<InitiativeLeadTriageDTO>();
            }
        }

        public async Task<bool> LinkLeadToCompanyAsync(int id, int idLead, int idCompany)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    $"{ConstHelper.InitiativesPath}/{id}/leads/{idLead}/link/{idCompany}", null);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<List<UserAwayDTO>> GetAwayUsersAsync(DateTime from, DateTime to)
        {
            try
            {
                var query = $"?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
                return await _httpClient.GetFromJsonAsync<List<UserAwayDTO>>(
                    $"{ConstHelper.InitiativesPath}/away{query}") ?? new List<UserAwayDTO>();
            }
            catch (Exception ex)
            {
                // Il segnale di assenza e' un aiuto, non un vincolo: se non arriva, l'assegnazione
                // resta possibile come prima.
                Console.WriteLine(ex.Message);
                return new List<UserAwayDTO>();
            }
        }

        public async Task<APIResponseMessage<InitiativeDTO>> CloseAsync(int id, string? closingNotes)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{ConstHelper.InitiativesPath}/{id}/close",
                new { ClosingNotes = closingNotes });

            if (!response.IsSuccessStatusCode)
            {
                return new APIResponseMessage<InitiativeDTO>
                {
                    State = false,
                    Code = response.StatusCode,
                    Message = await response.Content.ReadAsStringAsync()
                };
            }

            return JsonSerializer.Deserialize<APIResponseMessage<InitiativeDTO>>(
                await response.Content.ReadAsStringAsync(), JsonOptions)
                ?? new APIResponseMessage<InitiativeDTO> { State = false };
        }

        public async Task<List<InitiativeScheduleDTO>> GetSchedulesAsync(int id)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<InitiativeScheduleDTO>>($"{ConstHelper.InitiativesPath}/{id}/schedules")
                    ?? new List<InitiativeScheduleDTO>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return new List<InitiativeScheduleDTO>();
            }
        }

        public async Task<APIResponseMessage<InitiativeScheduleDTO>> SaveScheduleAsync(int id, InitiativeScheduleDTO schedule)
        {
            var response = schedule.Id > 0
                ? await _httpClient.PutAsJsonAsync($"{ConstHelper.InitiativesPath}/{id}/schedules/{schedule.Id}", schedule)
                : await _httpClient.PostAsJsonAsync($"{ConstHelper.InitiativesPath}/{id}/schedules", schedule);

            if (!response.IsSuccessStatusCode)
            {
                return new APIResponseMessage<InitiativeScheduleDTO>
                {
                    State = false,
                    Code = response.StatusCode,
                    Message = await response.Content.ReadAsStringAsync()
                };
            }

            return JsonSerializer.Deserialize<APIResponseMessage<InitiativeScheduleDTO>>(
                await response.Content.ReadAsStringAsync(), JsonOptions)
                ?? new APIResponseMessage<InitiativeScheduleDTO> { State = false };
        }

        public async Task<bool> DeleteScheduleAsync(int id, int idSchedule)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{ConstHelper.InitiativesPath}/{id}/schedules/{idSchedule}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
