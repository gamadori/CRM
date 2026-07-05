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
    public class OrdersController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly IOrdersService _ordersService;

        public OrdersController(ILogEventService logEventService, IOrdersService ordersService)
        {
            _logEventService = logEventService;
            _ordersService = ordersService;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<OrderDTO, decimal>>> Get([FromQuery] OrderFilter args)
        {
            try
            {
                var orders = await _ordersService.GetSummaryAsync(args);
                return orders ?? new PagingResponse<OrderDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<OrderDTO>?> GetItems([FromQuery] OrderFilter? args = null)
        {
            try
            {
                var items = await _ordersService.GetListAsync(args);
                return items ?? Enumerable.Empty<OrderDTO>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<OrderDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _ordersService.GetItemAsync(id);
                if (item == null)
                    return NotFound();
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<OrderDTO>>> Put(int id, Order item)
        {
            if (id != item.Id)
                return BadRequest();
            var resp = await _ordersService.PostAsync(item);
            if (resp == null)
                return Problem("Errore nel salvataggio dell'ordine");
            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<OrderDTO>>> Post(Order item)
        {
            var resp = await _ordersService.PostAsync(item);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");
            return Ok(resp);
        }

        [HttpPost("from-quote/{quoteId}")]
        public async Task<ActionResult<APIResponseMessage<OrderDTO>>> CreateFromQuote(int quoteId)
        {
            var resp = await _ordersService.CreateFromQuoteAsync(quoteId);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Create from quote return null");
            return Ok(resp);
        }

        [HttpPost("{id}/state")]
        public async Task<ActionResult<APIResponseMessage<OrderDTO>>> ChangeState(int id, [FromQuery] OrderStates state)
        {
            var resp = await _ordersService.ChangeStateAsync(id, state);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "State change return null");
            return Ok(resp);
        }

        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                var pdf = await _ordersService.GeneratePdfAsync(id);
                if (pdf == null)
                    return NotFound();
                return File(pdf.Value.Bytes, "application/pdf", pdf.Value.FileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(OrdersController), nameof(GetPdf), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _ordersService.DeleteAsync(id);
            if (!resp)
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore nell'eliminazione dell'ordine");
            return NoContent();
        }
    }
}
