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
    public class LeadsController : ControllerBase
    {
        private readonly ILeadsService _leadsService;
        private readonly ILogEventService _logEventService;

        public LeadsController(ILeadsService leadsService, ILogEventService logEventService)
        {
            _leadsService = leadsService;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<LeadDTO, decimal>>> Get([FromQuery] LeadFilter args)
        {
            try
            {
                return await _leadsService.GetSummaryAsync(args) ?? new PagingResponse<LeadDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(LeadsController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<LeadDTO>> GetList([FromQuery] LeadFilter? args = null)
        {
            return await _leadsService.GetListAsync(args) ?? Enumerable.Empty<LeadDTO>();
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<LeadDTO?>> GetItem(int id)
        {
            var item = await _leadsService.GetItemAsync(id);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<LeadDTO>>> Post(Lead item)
        {
            return Ok(await _leadsService.PostAsync(item));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<APIResponseMessage<LeadDTO>>> Put(int id, Lead item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }

            return Ok(await _leadsService.PostAsync(item));
        }

        [HttpPost("{id:int}/convert")]
        public async Task<ActionResult<APIResponseMessage<DealDTO>>> Convert(int id, ConvertLeadRequest request)
        {
            return Ok(await _leadsService.ConvertAsync(id, request));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            return await _leadsService.DeleteAsync(id) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
