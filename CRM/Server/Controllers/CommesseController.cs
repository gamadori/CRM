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
    public class CommesseController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly ICommesseService _service;

        public CommesseController(ILogEventService logEventService, ICommesseService service)
        {
            _logEventService = logEventService;
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<CommessaDTO, int>>> Get([FromQuery] CommessaFilter args)
        {
            var res = await _service.GetSummaryAsync(args);
            return res ?? new PagingResponse<CommessaDTO, int>();
        }

        [HttpGet("list")]
        public async Task<IEnumerable<CommessaDTO>?> GetItems([FromQuery] CommessaFilter? args = null)
            => (await _service.GetListAsync(args)) ?? Enumerable.Empty<CommessaDTO>();

        [HttpGet("{id}")]
        public async Task<ActionResult<CommessaDTO?>> GetItem(int id)
        {
            var item = await _service.GetItemAsync(id);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpGet("order/{orderId}")]
        public async Task<ActionResult<IEnumerable<CommessaDTO>>> GetByOrder(int orderId)
            => Ok(await _service.GetByOrderAsync(orderId));

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> Post(Commessa item)
            => Ok(await _service.PostAsync(item));

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> Put(int id, Commessa item)
        {
            if (id != item.Id) return BadRequest();
            return Ok(await _service.PostAsync(item));
        }

        [HttpPost("{id}/state")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> ChangeState(int id, [FromQuery] CommessaStates state)
            => Ok(await _service.ChangeStateAsync(id, state));

        [HttpPost("from-orderrow/{orderRowId}")]
        public async Task<ActionResult<APIResponseMessage<List<CommessaDTO>>>> StartProduction(int orderRowId)
            => Ok(await _service.StartProductionAsync(orderRowId));

        [HttpPost("internal")]
        public async Task<ActionResult<APIResponseMessage<List<CommessaDTO>>>> StartInternalProduction(InternalProductionRequestDTO req)
            => Ok(await _service.StartInternalProductionAsync(req));

        [HttpPost("orderrow/{orderRowId}/ready")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> ConfirmRowReady(int orderRowId)
            => Ok(await _service.ConfirmRowReadyAsync(orderRowId));

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
            => await _service.DeleteAsync(id) ? NoContent() : StatusCode(StatusCodes.Status500InternalServerError);
    }
}
