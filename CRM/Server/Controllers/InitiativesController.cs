using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InitiativesController : ControllerBase
    {
        private readonly IInitiativesService _initiativesService;
        private readonly ILogEventService _logEventService;

        public InitiativesController(IInitiativesService initiativesService, ILogEventService logEventService)
        {
            _initiativesService = initiativesService;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<InitiativeDTO, decimal>>> Get([FromQuery] InitiativeFilter args)
        {
            try
            {
                return await _initiativesService.GetSummaryListAsync(args) ?? new PagingResponse<InitiativeDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InitiativesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<InitiativeDTO>> GetList([FromQuery] InitiativeFilter? args = null)
        {
            return await _initiativesService.GetListAsync(args) ?? Enumerable.Empty<InitiativeDTO>();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<InitiativeDTO?>> GetItem(int id)
        {
            var item = await _initiativesService.GetItemAsync(id);
            return item == null ? NotFound() : Ok(item);
        }

        /// <summary>Il resoconto dell'iniziativa: costi, cosa e' successo, cosa si e' aperto.</summary>
        [HttpGet("{id:int}/report")]
        public async Task<ActionResult<InitiativeSummaryDTO?>> GetReport(int id)
        {
            var report = await _initiativesService.GetReportAsync(id);
            return report == null ? NotFound() : Ok(report);
        }

        /// <summary>I biglietti raccolti, con cosa manca e a quale azienda gia' nota somigliano.</summary>
        [HttpGet("{id:int}/leads/triage")]
        public async Task<ActionResult<IEnumerable<InitiativeLeadTriageDTO>>> GetLeadTriage(int id)
        {
            return Ok(await _initiativesService.GetLeadTriageAsync(id));
        }

        [HttpPost("{id:int}/leads/{idLead:int}/link/{idCompany:int}")]
        public async Task<IActionResult> LinkLead(int id, int idLead, int idCompany)
        {
            return await _initiativesService.LinkLeadToCompanyAsync(id, idLead, idCompany) ? NoContent() : NotFound();
        }

        /// <summary>
        /// Chi e' impegnato in un'iniziativa nel periodo indicato. Serve a chi assegna un ticket e
        /// non apre l'agenda.
        /// </summary>
        [HttpGet("away")]
        public async Task<ActionResult<IEnumerable<UserAwayDTO>>> GetAway([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            // Giornate intere: chi chiede "chi e' fuori il 12" passa una data secca, e confrontarla
            // a mezzanotte escluderebbe tutto il giorno che voleva sapere.
            var start = (from ?? DateTime.Today).Date;
            var end = (to ?? start).Date.AddDays(1).AddSeconds(-1);

            return Ok(await _initiativesService.GetAwayUsersAsync(start, end));
        }

        [HttpGet("{id:int}/schedules")]
        public async Task<ActionResult<IEnumerable<InitiativeScheduleDTO>>> GetSchedules(int id)
        {
            return Ok(await _initiativesService.GetSchedulesAsync(id));
        }

        [HttpPost("{id:int}/schedules")]
        public async Task<ActionResult<APIResponseMessage<InitiativeScheduleDTO>>> SaveSchedule(int id, InitiativeScheduleDTO schedule)
        {
            return Ok(await _initiativesService.SaveScheduleAsync(id, schedule));
        }

        [HttpPut("{id:int}/schedules/{idSchedule:int}")]
        public async Task<ActionResult<APIResponseMessage<InitiativeScheduleDTO>>> UpdateSchedule(int id, int idSchedule, InitiativeScheduleDTO schedule)
        {
            if (idSchedule != schedule.Id)
                return BadRequest();

            return Ok(await _initiativesService.SaveScheduleAsync(id, schedule));
        }

        [HttpDelete("{id:int}/schedules/{idSchedule:int}")]
        public async Task<IActionResult> DeleteSchedule(int id, int idSchedule)
        {
            return await _initiativesService.DeleteScheduleAsync(id, idSchedule) ? NoContent() : NotFound();
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<InitiativeDTO>>> Post(Initiative item)
        {
            return Ok(await _initiativesService.PostAsync(item));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<APIResponseMessage<InitiativeDTO>>> Put(int id, Initiative item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            return Ok(await _initiativesService.PostAsync(item));
        }

        [HttpPost("{id:int}/close")]
        public async Task<ActionResult<APIResponseMessage<InitiativeDTO>>> Close(int id, [FromBody] CloseInitiativeRequest? request = null)
        {
            return Ok(await _initiativesService.CloseAsync(id, request?.ClosingNotes));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _initiativesService.DeleteAsync(id) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    public class CloseInitiativeRequest
    {
        public string? ClosingNotes { get; set; }
    }
}
