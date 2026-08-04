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

        /// <summary>Apre una commessa a fasi libere da una riga senza template di produzione.</summary>
        [HttpPost("open-from-orderrow")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> OpenFromOrderRow(OpenCommessaRequestDTO req)
            => Ok(await _service.OpenCommessaFromOrderRowAsync(req));

        [HttpPost("internal")]
        public async Task<ActionResult<APIResponseMessage<List<CommessaDTO>>>> StartInternalProduction(InternalProductionRequestDTO req)
            => Ok(await _service.StartInternalProductionAsync(req));

        /// <summary>Sposta il piano su una nuova consegna traslando le fasi (vedi RescheduleAsync).</summary>
        [HttpPost("{id}/reschedule")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> Reschedule(int id, [FromQuery] DateTime delivery)
            => Ok(await _service.RescheduleAsync(id, delivery));

        /// <summary>Ricostruisce le fasi dal template del prodotto, scartando le modifiche al piano.</summary>
        [HttpPost("{id}/rebuild-plan")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> RebuildPlan(int id, [FromQuery] DateTime? delivery = null)
            => Ok(await _service.RebuildPlanFromTemplateAsync(id, delivery));

        [HttpPost("orderrow/{orderRowId}/ready")]
        public async Task<ActionResult<APIResponseMessage<CommessaDTO>>> ConfirmRowReady(int orderRowId)
            => Ok(await _service.ConfirmRowReadyAsync(orderRowId));

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Lo stato HTTP segue l'esito reale: 404 se non esiste, 403 se fuori perimetro,
            // 500 solo per un errore vero. Il client distingue via IsSuccessStatusCode.
            var resp = await _service.DeleteAsync(id);
            return resp.State ? NoContent() : StatusCode((int)resp.Code, resp);
        }
    }
}
