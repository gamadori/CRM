using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/external/tickets")]
    public class ExternalTicketsController : ControllerBase
    {
        private const string ApiKeyHeader = "X-Api-Key";
        private readonly IExternalTicketApiService _service;
        private readonly IApiKeyService _apiKeys;
        private readonly ILogEventService _logEventService;

        public ExternalTicketsController(IExternalTicketApiService service, IApiKeyService apiKeys, ILogEventService logEventService)
        {
            _service = service;
            _apiKeys = apiKeys;
            _logEventService = logEventService;
        }

        [HttpPost]
        public async Task<ActionResult<ExternalTicketResponse>> Create(ExternalTicketCreateRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var apiKey = await AuthorizeAsync();
                if (apiKey == null)
                {
                    return Unauthorized();
                }

                var ticket = await _service.CreateTicketAsync(apiKey, request);
                return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ExternalTicketsController), nameof(Create), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ExternalTicketResponse>> GetById(int id)
        {
            try
            {
                var apiKey = await AuthorizeAsync();
                if (apiKey == null)
                {
                    return Unauthorized();
                }

                var ticket = await _service.GetTicketAsync(apiKey, id);
                return ticket == null ? NotFound() : Ok(ticket);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ExternalTicketsController), nameof(GetById), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<ExternalTicketResponse>>> GetList(
            [FromQuery] bool includeClosed = false,
            [FromQuery] int skip = 0,
            [FromQuery] int top = 50)
        {
            try
            {
                var apiKey = await AuthorizeAsync();
                if (apiKey == null)
                {
                    return Unauthorized();
                }

                var tickets = await _service.GetTicketsAsync(apiKey, includeClosed, skip, top);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ExternalTicketsController), nameof(GetList), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        /// <summary>
        /// Allega un file a un ticket gia' aperto con la stessa chiave: il pannello macchina lo
        /// usa per mandare lo storico allarmi delle ultime 24 ore insieme alla richiesta. Un
        /// ticket di un'altra azienda non si vede e risponde 404, come nella lettura.
        /// </summary>
        [HttpPost("{id:int}/attachments")]
        [RequestSizeLimit(20_971_520)]
        public async Task<ActionResult<ExternalTicketAttachmentResponse>> Attach(
            int id,
            IFormFile file,
            [FromForm] string? description,
            CancellationToken cancellationToken)
        {
            try
            {
                var apiKey = await AuthorizeAsync();
                if (apiKey == null)
                {
                    return Unauthorized();
                }

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { message = "Nessun file allegato." });
                }

                byte[] content;
                await using (var stream = file.OpenReadStream())
                await using (var buffer = new MemoryStream())
                {
                    await stream.CopyToAsync(buffer, cancellationToken);
                    content = buffer.ToArray();
                }

                var result = await _service.AttachFileAsync(apiKey, id, file.FileName, file.ContentType, content, description);
                return result == null ? NotFound() : Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ExternalTicketsController), nameof(Attach), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        private Task<ApiKey?> AuthorizeAsync()
        {
            var key = Request.Headers.TryGetValue(ApiKeyHeader, out var values)
                ? values.FirstOrDefault()
                : null;

            if (string.IsNullOrWhiteSpace(key))
            {
                var authorization = Request.Headers.Authorization.FirstOrDefault();
                const string bearer = "Bearer ";
                if (!string.IsNullOrWhiteSpace(authorization) &&
                    authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
                {
                    key = authorization.Substring(bearer.Length);
                }
            }

            return _apiKeys.ValidateAsync(key, ApiKeyScope.ExternalTicket);
        }
    }
}
