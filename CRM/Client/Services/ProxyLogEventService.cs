using CRM.Client.Helpers;
using CRM.Shared;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Client.Services
{
    public class ProxyLogEventService : ProxyRestClientService<LogEvent, int, LogEventFilterModel, object>, ILogEventService
    {

        public ProxyLogEventService(HttpClient http) : base(http, ConstHelper.LogEventsPath)
        {
         
        }

        public void Register(string module, string subroutine, EventsTypes type, string massage)
        {
            // Chiamata asincrona "fire and forget"
            _ = RegisterAsync(module, subroutine, type, massage);
        }

        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, string massage)
        {
            var payload = new
            {
                Module = module,
                Subroutine = subroutine,
                Type = type,
                Massage = massage
            };
            await _http.PostAsJsonAsync("api/LogEvent", payload);
        }

        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, Exception ex)
        {
            var payload = new
            {
                Module = module,
                Subroutine = subroutine,
                Type = type,
                Massage = ex.Message,
                Exception = ex.ToString()
            };
            await _http.PostAsJsonAsync("api/LogEvent/exception", payload);
        }
    }
}