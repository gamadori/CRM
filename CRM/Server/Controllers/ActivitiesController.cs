using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivitiesController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly IActivitiesService _activitiesService;

        public ActivitiesController(ILogEventService logEventService, IActivitiesService activitiesService)
        {
            _logEventService = logEventService;
            _activitiesService = activitiesService;
        }

        [HttpGet("by-entity/{entityType}/{entityId}")]
        public async Task<ActionResult<IEnumerable<ActivityDTO>>> GetByEntity(ActivityEntityType entityType, int entityId)
        {
            try
            {
                var items = await _activitiesService.GetByEntityAsync(entityType, entityId);
                return Ok(items ?? new List<ActivityDTO>());
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesController), nameof(GetByEntity), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("my-agenda")]
        public async Task<ActionResult<IEnumerable<ActivityDTO>>> GetMyAgenda([FromQuery] ActivityFilter? args = null)
        {
            try
            {
                var items = await _activitiesService.GetMyAgendaAsync(args);
                return Ok(items ?? new List<ActivityDTO>());
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesController), nameof(GetMyAgenda), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ActivityDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _activitiesService.GetItemAsync(id);
                if (item == null)
                    return NotFound();
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ActivityDTO>>> Put(int id, Activity item)
        {
            if (id != item.Id)
                return BadRequest();
            var resp = await _activitiesService.PostAsync(item);
            if (resp == null)
                return Problem("Errore nel salvataggio dell'attivita'");
            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ActivityDTO>>> Post(Activity item)
        {
            var resp = await _activitiesService.PostAsync(item);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");
            return Ok(resp);
        }

        [HttpPost("{id}/complete")]
        public async Task<ActionResult<APIResponseMessage<ActivityDTO>>> Complete(int id, ActivityCompletionRequest? completion = null)
        {
            var resp = await _activitiesService.CompleteAsync(id, completion);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Complete return null");
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _activitiesService.DeleteAsync(id);
            if (!resp)
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore nell'eliminazione dell'attivita'");
            return NoContent();
        }
    }
}
