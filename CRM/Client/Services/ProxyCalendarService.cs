using CRM.Client.Helpers;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyCalendarService : ICalendarService
    {
        private readonly HttpClient _http;

        public ProxyCalendarService(HttpClient http)
        {
            _http = http;
        }

        public async Task<CalendarAgendaDTO> GetAgendaAsync(CalendarFilter filter)
        {
            try
            {
                var query = new List<string>();

                if (filter.DateFrom.HasValue)
                    query.Add($"dateFrom={Uri.EscapeDataString(filter.DateFrom.Value.ToString("O"))}");

                if (filter.DateTo.HasValue)
                    query.Add($"dateTo={Uri.EscapeDataString(filter.DateTo.Value.ToString("O"))}");

                if (!string.IsNullOrWhiteSpace(filter.IdUser))
                    query.Add($"idUser={Uri.EscapeDataString(filter.IdUser)}");

                query.Add($"scope={filter.Scope}");

                if (filter.Source.HasValue)
                    query.Add($"source={filter.Source.Value}");
                else
                {
                    query.Add($"includeActivities={filter.IncludeActivities.ToString().ToLowerInvariant()}");
                    query.Add($"includeTickets={filter.IncludeTickets.ToString().ToLowerInvariant()}");
                }

                var url = $"{ConstHelper.CalendarPath}/agenda";
                if (query.Count > 0)
                    url += "?" + string.Join("&", query);

                return await _http.GetFromJsonAsync<CalendarAgendaDTO>(url) ?? new CalendarAgendaDTO();
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new CalendarAgendaDTO();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento calendario: {ex.Message}");
                return new CalendarAgendaDTO();
            }
        }
    }
}
