using System.Text;
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
    public class InvoicesController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly IInvoicesService _invoicesService;

        public InvoicesController(ILogEventService logEventService, IInvoicesService invoicesService)
        {
            _logEventService = logEventService;
            _invoicesService = invoicesService;
        }

        [HttpGet]
        public async Task<ActionResult<PagingResponse<InvoiceDTO, decimal>>> Get([FromQuery] InvoiceFilter args)
        {
            try
            {
                var invoices = await _invoicesService.GetSummaryAsync(args);
                return invoices ?? new PagingResponse<InvoiceDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<InvoiceDTO>?> GetItems([FromQuery] InvoiceFilter? args = null)
        {
            try
            {
                var items = await _invoicesService.GetListAsync(args);
                return items ?? Enumerable.Empty<InvoiceDTO>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<InvoiceDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _invoicesService.GetItemAsync(id);
                if (item == null)
                    return NotFound();
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<InvoiceDTO>>> Put(int id, Invoice item)
        {
            if (id != item.Id)
                return BadRequest();
            var resp = await _invoicesService.PostAsync(item);
            if (resp == null)
                return Problem("Errore nel salvataggio della fattura");
            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<InvoiceDTO>>> Post(Invoice item)
        {
            var resp = await _invoicesService.PostAsync(item);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");
            return Ok(resp);
        }

        [HttpPost("from-order/{orderId}")]
        public async Task<ActionResult<APIResponseMessage<InvoiceDTO>>> CreateFromOrder(int orderId)
        {
            var resp = await _invoicesService.CreateFromOrderAsync(orderId);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Create from order return null");
            return Ok(resp);
        }

        [HttpPut("{id}/recipient")]
        public async Task<ActionResult<APIResponseMessage<InvoiceDTO>>> UpdateRecipient(int id, InvoiceRecipientDTO payload)
        {
            var resp = await _invoicesService.UpdateRecipientAsync(id, payload?.CodiceDestinatario);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Update recipient return null");
            return Ok(resp);
        }

        [HttpGet("{id}/xml")]
        public async Task<IActionResult> GetXml(int id)
        {
            try
            {
                var generated = await _invoicesService.GenerateXmlAsync(id);
                if (generated == null)
                    return NotFound();

                var bytes = Encoding.UTF8.GetBytes(generated.Value.Xml);
                return File(bytes, "application/xml", generated.Value.FileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InvoicesController), nameof(GetXml), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{id}/send")]
        public async Task<ActionResult<APIResponseMessage<InvoiceDTO>>> Send(int id)
        {
            var resp = await _invoicesService.SendAsync(id);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Send return null");
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _invoicesService.DeleteAsync(id);
            if (!resp)
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore nell'eliminazione della fattura");
            return NoContent();
        }
    }
}
