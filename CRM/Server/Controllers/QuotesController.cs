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
    public class QuotesController : ControllerBase
    {
        private readonly ILogEventService _logEventService;
        private readonly IQuotesService _quotesService;

        public QuotesController(ILogEventService logEventService, IQuotesService quotesService)
        {
            _logEventService = logEventService;
            _quotesService = quotesService;
        }

        // GET: api/Quotes
        [HttpGet]
        public async Task<ActionResult<PagingResponse<QuoteDTO, decimal>>> Get([FromQuery] QuoteFilter args)
        {
            try
            {
                var quotes = await _quotesService.GetSummaryAsync(args);
                return quotes ?? new PagingResponse<QuoteDTO, decimal>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<QuoteDTO>?> GetItems([FromQuery] QuoteFilter? args = null)
        {
            try
            {
                var items = await _quotesService.GetListAsync(args);
                return items ?? Enumerable.Empty<QuoteDTO>();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<QuoteDTO>();
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<QuoteDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _quotesService.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<QuoteDTO>>> Put(int id, Quote item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _quotesService.PostAsync(item);
            if (resp == null)
                return Problem("Errore nel salvataggio del preventivo");
            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<QuoteDTO>>> Post(Quote item)
        {
            var resp = await _quotesService.PostAsync(item);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");
            return Ok(resp);
        }

        [HttpPost("{id}/state")]
        public async Task<ActionResult<APIResponseMessage<QuoteDTO>>> ChangeState(int id, [FromQuery] QuoteStates state, [FromQuery] bool updateDeal = true)
        {
            var resp = await _quotesService.ChangeStateAsync(id, state, updateDeal);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "State change return null");
            return Ok(resp);
        }

        [HttpPost("{id}/revision")]
        public async Task<ActionResult<APIResponseMessage<QuoteDTO>>> CreateRevision(int id)
        {
            var resp = await _quotesService.CreateRevisionAsync(id);
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Create revision return null");
            return Ok(resp);
        }

        [HttpGet("{id}/revisions")]
        public async Task<ActionResult<IEnumerable<QuoteRevisionDTO>>> GetRevisions(int id)
            => Ok(await _quotesService.GetRevisionsAsync(id));

        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetPdf(int id)
        {
            try
            {
                // Se il preventivo è già stato inviato restituisce lo snapshot congelato
                var pdf = await _quotesService.GetPdfAsync(id);
                if (pdf == null)
                    return NotFound();

                return File(pdf.Value.Bytes, "application/pdf", pdf.Value.FileName);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(QuotesController), nameof(GetPdf), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("{id}/send")]
        public async Task<ActionResult<APIResponseMessage<QuoteDTO>>> Send(int id, QuoteSendRequest request)
        {
            var resp = await _quotesService.SendAsync(id, request ?? new QuoteSendRequest());
            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Send return null");
            return Ok(resp);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _quotesService.DeleteAsync(id);
            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Errore nell'eliminazione del preventivo");
            }
            return NoContent();
        }
    }
}
