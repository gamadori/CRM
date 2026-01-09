using CRM.Shared;
using System;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Client.Services
{
    public interface ILogEventService : IDataService<LogEvent, int, LogEventFilterModel, object>
    {
        void Register(string module, string subroutine, EventsTypes type, string massage);

        Task RegisterAsync(string module, string subroutine, EventsTypes type, string massage);

        Task RegisterAsync(string module, string subroutine, EventsTypes type, Exception ex);
    }
}
