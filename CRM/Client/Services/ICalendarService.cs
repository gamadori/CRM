using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ICalendarService
    {
        Task<CalendarAgendaDTO> GetAgendaAsync(CalendarFilter filter);
    }
}
