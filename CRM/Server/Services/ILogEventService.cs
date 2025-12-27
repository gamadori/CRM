using CRM.Shared;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public interface ILogEventService
    {
        void Register(string module, string subroutine, EventsTypes type, string massage, ActivityType? activityType = null);

        Task RegisterAsync(string module, string subroutine, EventsTypes type, string massage, ActivityType? activityType = null);

        Task RegisterAsync(string module, string subroutine, EventsTypes type, Exception ex, ActivityType? activityType = null);
    }
}
