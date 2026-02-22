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
    public class InterventionTypeLangsController : ControllerBase
    {
        private readonly IInterventionTypeLangsService _service;

        private readonly ILogEventService _logEventService;

        private readonly IPermitsService _permitsService;
        public InterventionTypeLangsController(IInterventionTypeLangsService service, ILogEventService logEventService, IPermitsService permitsService)
        {
            _service = service;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }

        [HttpGet]
        public async Task<PagingResponse<InterventionTypeLangDTO>?> GetPage([FromQuery] InterventionTypeLangFilter? args = null)
        {
            try
            {
                
                var items = await _service.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypeLangsController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<InterventionTypeLangDTO>?> GetItems([FromQuery] InterventionTypeLangFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<InterventionTypeLangDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionTypeLangsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<InterventionTypeLangDTO>();
            }
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<InterventionTypeLangDTO?>> GetItem(int id)
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
                await _logEventService.RegisterAsync(nameof(InterventionTypeLangsController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        // PUT: api/InterventionTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<InterventionTypeLangDTO>>> Put(int id, InterventionTypeLanguage item)
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
        public async Task<ActionResult<APIResponseMessage<InterventionTypeLangDTO>>> Post(InterventionTypeLanguage item)
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

        [HttpGet("Flag/{id}")]  
        public async Task<ActionResult<string?>> GetFlag(int id)
        {             
            var flag = await _service.GetFlagAsync(id);

            if (flag == null)
            {
                return NotFound();
            }
            return Ok(flag);
        }


    }
}
