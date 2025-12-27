using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    public class LogEventService: ILogEventService
    {       
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public LogEventService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor; 
        }


        public void Register(string module, string subroutine, EventsTypes type, string message, ActivityType? activityType = null)
        {
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, message, activityType);

            _context.LogEvents.Add(logEvent);

            _context.SaveChanges();
        }

        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, string message, ActivityType? activityType = null)
        {
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, message, activityType);
            
            _context.LogEvents.Add(logEvent);

            await _context.SaveChangesAsync();
        }
        public async Task RegisterAsync(string module, string subroutine, EventsTypes type, Exception ex, ActivityType? activityType = null)
        {
            string msg = $"{ex.Message}\n\r{ex.StackTrace}";
            LogEvent logEvent = CreateLogEvent(module, subroutine, type, msg, activityType);

            _context.LogEvents.Add(logEvent);

            await _context.SaveChangesAsync();
        }
        

        private LogEvent CreateLogEvent(string module, string subroutine, EventsTypes type, string massage, ActivityType? activityType = null)
        {
            LogEvent logEvent = new LogEvent();

            logEvent.Module = module;
            logEvent.Subroutine = subroutine;
            logEvent.EventType = type;
            logEvent.Message = massage;
            logEvent.DateEvent = DateTime.Now;
            logEvent.User = _httpContextAccessor?.HttpContext?.User?.Identity?.Name;
            logEvent.UserId = _httpContextAccessor?.HttpContext?.User?.FindFirst("sub")?.Value;
            logEvent.ActivityType = activityType;

            return logEvent;
        }
    }
}
