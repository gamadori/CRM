using System;
using System.Threading;
using System.Threading.Tasks;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Endpoint dell'assistente "dati CRM" (Funzione 2): risponde a domande sui dati
    /// aziendali (clienti, macchine, ticket) tramite tool-use di Claude.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CrmAssistantController : ControllerBase
    {
        private readonly CrmDataAssistantService _assistant;
        private readonly ILogEventService _logEventService;

        public CrmAssistantController(
            CrmDataAssistantService assistant,
            ILogEventService logEventService)
        {
            _assistant = assistant;
            _logEventService = logEventService;
        }

        [HttpPost("ask")]
        public async Task<ActionResult<AssistantChatResponse>> Ask(
            [FromBody] AssistantChatRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (request?.Messages == null || request.Messages.Count == 0)
                    return BadRequest("La conversazione è vuota");

                var reply = await _assistant.AskAsync(request.Messages, cancellationToken);

                return new AssistantChatResponse { Reply = reply, Success = true };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CrmAssistantController), nameof(Ask), LogEvent.EventsTypes.Error, ex);
                return Ok(new AssistantChatResponse
                {
                    Success = false,
                    Message = $"Errore durante l'elaborazione: {ex.Message}"
                });
            }
        }
    }
}
