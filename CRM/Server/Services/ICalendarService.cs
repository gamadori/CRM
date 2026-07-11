using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ICalendarService
    {
        Task<CalendarAgendaDTO> GetAgendaAsync(CalendarFilter filter);
    }
}
