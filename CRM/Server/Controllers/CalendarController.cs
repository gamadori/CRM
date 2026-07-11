using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CalendarController : ControllerBase
    {
        private readonly ICalendarService _calendarService;

        public CalendarController(ICalendarService calendarService)
        {
            _calendarService = calendarService;
        }

        [HttpGet("agenda")]
        public async Task<ActionResult<CalendarAgendaDTO>> GetAgenda([FromQuery] CalendarFilter filter)
        {
            return Ok(await _calendarService.GetAgendaAsync(filter));
        }
    }
}
