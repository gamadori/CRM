using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class InterventionTypesController : ControllerBase
    {
        private readonly IInterventionTypesService _service;

        private readonly ILogEventService _logEventService;

        private readonly IPermitsService _permitsService;
        public InterventionTypesController(IInterventionTypesService service, ILogEventService logEventService, IPermitsService permitsService)
        {
            _service = service;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }

        [HttpGet]
        public async Task<PagingResponse<InterventionTypeDTO>?> GetPage([FromQuery] InterventionTypeFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<InterventionTypeDTO>?> GetItems([FromQuery] InterventionTypeFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<InterventionTypeDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<InterventionTypeDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionTypeDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _service.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }


        // PUT: api/InterventionTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<InterventionTypeDTO>>> Put(int id, InterventionType item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving settings");

            return Ok(resp);
        }

        // POST: api/InterventionTypes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<InterventionTypeDTO>>> Post(InterventionType item)
        {
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        // DELETE: api/InterventionTypes/5
        //[AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Logo");
            }
            else
                return NoContent();
        }

        [HttpGet("translate/{id}")]
        public async Task<ActionResult<string>> Translate(int id)
        {
            try
            {
                return Ok(await _service.Translate(id));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypesController), nameof(Translate), LogEvent.EventsTypes.Error, ex);
                return string.Empty;
            }
        }


    }
}
