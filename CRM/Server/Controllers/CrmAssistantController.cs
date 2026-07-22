using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Endpoint dell'assistente AI unificato: chat in streaming (dati CRM + soluzioni dai
    /// ticket chiusi/knowledge base), feedback dell'operatore e consultazione dei log Q&amp;A.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CrmAssistantController : ControllerBase
    {
        /// <summary>Serializzazione degli eventi di stream: camelCase, niente campi nulli.</summary>
        private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>Limite di dimensione dell'audio caricato per la trascrizione (Whisper accetta fino a 25 MB).</summary>
        private const long MaxAudioBytes = 25 * 1024 * 1024;

        private readonly CrmAssistantService _assistant;
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IVoiceTranscriptionService _transcription;

        public CrmAssistantController(
            CrmAssistantService assistant,
            ApplicationDbContext context,
            ILogEventService logEventService,
            IVoiceTranscriptionService transcription)
        {
            _assistant = assistant;
            _context = context;
            _logEventService = logEventService;
            _transcription = transcription;
        }

        /// <summary>
        /// Chat con l'assistente in streaming NDJSON: una riga JSON per evento
        /// (<see cref="AssistantStreamEvent"/>). Il flusso termina alla chiusura della risposta.
        /// </summary>
        [HttpPost("chat-stream")]
        public async Task ChatStream(
            [FromBody] AssistantChatRequest request,
            CancellationToken cancellationToken)
        {
            Response.ContentType = "application/x-ndjson; charset=utf-8";

            if (request?.Messages == null || request.Messages.Count == 0)
            {
                await WriteEventAsync(new AssistantStreamEvent
                {
                    Type = AssistantStreamEvent.TypeError,
                    Text = "La conversazione è vuota"
                }, cancellationToken);
                return;
            }

            try
            {
                await foreach (var streamEvent in _assistant
                    .ChatStreamAsync(request, cancellationToken))
                {
                    await WriteEventAsync(streamEvent, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnesso: nessuna azione necessaria
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CrmAssistantController), nameof(ChatStream), LogEvent.EventsTypes.Error, ex);
                try
                {
                    await WriteEventAsync(new AssistantStreamEvent
                    {
                        Type = AssistantStreamEvent.TypeError,
                        Text = $"Errore durante l'elaborazione: {ex.Message}"
                    }, cancellationToken);
                }
                catch { /* stream già chiuso */ }
            }
        }

        private async Task WriteEventAsync(AssistantStreamEvent streamEvent, CancellationToken cancellationToken)
        {
            var line = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);
            await Response.WriteAsync(line + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Trascrive in testo l'audio registrato dal microfono (modalità vocale "Server"/Whisper).
        /// Attivo solo se l'amministratore ha scelto quella modalità nei Settaggi globali: è la
        /// stessa impostazione che abilita il pulsante microfono lato client, verificata anche qui
        /// per non esporre un endpoint a consumo quando la funzione è disattivata.
        /// </summary>
        [HttpPost("transcribe")]
        [RequestSizeLimit(MaxAudioBytes + 1024)]
        public async Task<IActionResult> Transcribe(IFormFile audio, CancellationToken cancellationToken)
        {
            try
            {
                var settings = await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
                if (settings?.VoiceInputMode != VoiceInputMode.Whisper)
                    return BadRequest("La trascrizione vocale sul server non è abilitata.");

                if (!_transcription.IsConfigured)
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, "Trascrizione vocale non configurata sul server.");

                if (audio == null || audio.Length == 0)
                    return BadRequest("Nessun audio ricevuto.");
                if (audio.Length > MaxAudioBytes)
                    return BadRequest("Audio troppo lungo per la trascrizione.");

                await using var stream = audio.OpenReadStream();
                var fileName = string.IsNullOrWhiteSpace(audio.FileName) ? "audio.webm" : audio.FileName;

                var text = await _transcription.TranscribeAsync(stream, fileName, cancellationToken);
                return Ok(new { text });
            }
            catch (OperationCanceledException)
            {
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CrmAssistantController), nameof(Transcribe), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante la trascrizione: {ex.Message}");
            }
        }

        /// <summary>Registra il voto di feedback dell'operatore su una risposta dell'assistente.</summary>
        [HttpPost("feedback")]
        public async Task<IActionResult> Feedback([FromBody] AssistantFeedbackRequest request)
        {
            try
            {
                if (request == null || request.LogId <= 0)
                    return BadRequest("Log non valido");

                var log = await _context.AssistantChatLogs.FirstOrDefaultAsync(l => l.Id == request.LogId);
                if (log == null)
                    return NotFound();

                log.Feedback = request.Vote >= 0 ? AssistantFeedbackVote.Up : AssistantFeedbackVote.Down;
                log.FeedbackComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
                log.FeedbackAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CrmAssistantController), nameof(Feedback), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante il salvataggio del feedback: {ex.Message}");
            }
        }

        /// <summary>Elenco dei log Q&amp;A dell'assistente (consultazione admin).</summary>
        [HttpGet("logs")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<List<CRM.Shared.DTOs.AssistantChatLogDTO>>> GetLogs(
            [FromQuery] AssistantChatLogFilter filter)
        {
            try
            {
                var q = _context.AssistantChatLogs.AsNoTracking().AsQueryable();

                if (filter != null)
                {
                    if (!string.IsNullOrWhiteSpace(filter.Search))
                    {
                        var s = filter.Search;
                        q = q.Where(l => l.Question.Contains(s) || l.Answer.Contains(s));
                    }

                    if (filter.Vote.HasValue)
                    {
                        if (filter.Vote.Value == 0)
                            q = q.Where(l => l.Feedback == null);
                        else if (filter.Vote.Value > 0)
                            q = q.Where(l => l.Feedback == AssistantFeedbackVote.Up);
                        else
                            q = q.Where(l => l.Feedback == AssistantFeedbackVote.Down);
                    }
                }

                var rows = await q
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(500)
                    .Select(l => new
                    {
                        l.Id,
                        l.Question,
                        l.Answer,
                        UserName = l.User != null ? (l.User.Surname + " " + l.User.Name).Trim() : null,
                        l.IdTicket,
                        l.IdProduct,
                        l.ReferencedTicketsJson,
                        l.CreatedAt,
                        l.Feedback,
                        l.FeedbackComment,
                        l.FeedbackAt
                    })
                    .ToListAsync();

                return rows.Select(r => new CRM.Shared.DTOs.AssistantChatLogDTO
                {
                    Id = r.Id,
                    Question = r.Question,
                    Answer = r.Answer,
                    UserName = string.IsNullOrWhiteSpace(r.UserName) ? null : r.UserName,
                    IdTicket = r.IdTicket,
                    IdProduct = r.IdProduct,
                    ReferencedCount = CountReferencedTickets(r.ReferencedTicketsJson),
                    CreatedAt = r.CreatedAt,
                    Feedback = r.Feedback.HasValue ? (int?)(int)r.Feedback.Value : null,
                    FeedbackComment = r.FeedbackComment,
                    FeedbackAt = r.FeedbackAt
                }).ToList();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CrmAssistantController), nameof(GetLogs), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante il recupero dei log dell'assistente: {ex.Message}");
            }
        }

        private static int CountReferencedTickets(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return 0;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Array
                    ? doc.RootElement.GetArrayLength()
                    : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
