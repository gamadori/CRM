using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Controller per l'elaborazione automatica di scontrini e fatture tramite Azure Form Recognizer
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptProcessorController : ControllerBase
    {
        private readonly IReceiptProcessorService _receiptProcessor;
        private readonly ILogEventService _logEventService;
        private readonly ILogger<ReceiptProcessorController> _logger;

        public ReceiptProcessorController(
            IReceiptProcessorService receiptProcessor,
            ILogEventService logEventService,
            ILogger<ReceiptProcessorController> logger)
        {
            _receiptProcessor = receiptProcessor;
            _logEventService = logEventService;
            _logger = logger;
        }

        /// <summary>
        /// Elabora un file attachment caricato ed estrae i campi strutturati
        /// </summary>
        /// <param name="attachmentFileId">ID del file attachment</param>
        /// <param name="useCustomModel">Se true, usa modello custom invece di prebuilt-receipt</param>
        /// <param name="customModelId">ID del modello custom (opzionale)</param>
        /// <returns>Risultato dell'estrazione con campi estratti</returns>
        /// <response code="200">Estrazione completata con successo</response>
        /// <response code="400">File non valido o Azure Form Recognizer non configurato</response>
        /// <response code="404">File non trovato</response>
        /// <response code="500">Errore interno durante l'elaborazione</response>
        [HttpGet("process/{attachmentFileId}")]
        [ProducesResponseType(typeof(ReceiptExtractionResult), 200)]
        public async Task<ActionResult<ReceiptExtractionResult>> ProcessReceipt(
            int attachmentFileId,
            [FromQuery] bool useCustomModel = false,
            [FromQuery] string customModelId = null)
        {
            try
            {
                _logger.LogInformation(
                    "Richiesta elaborazione receipt per file {FileId} (custom model: {UseCustom})",
                    attachmentFileId,
                    useCustomModel);

                var result = await _receiptProcessor.ProcessReceiptAsync(
                    attachmentFileId,
                    useCustomModel,
                    customModelId);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Elaborazione fallita per file {FileId}: {Error}",
                        attachmentFileId,
                        result.ErrorMessage);

                    // Restituisci 200 con Success=false invece di 400
                    // (l'errore è gestito lato client tramite il campo Success)
                    return Ok(result);
                }

                await _logEventService.RegisterAsync(
                    nameof(ReceiptProcessorController),
                    nameof(ProcessReceipt),
                    LogEvent.EventsTypes.Info,
                    $"Receipt elaborato con successo: File={attachmentFileId}, Totale={result.TotalAmount}, Confidence={result.AverageConfidence:P}");

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione del receipt {FileId}", attachmentFileId);
                
                await _logEventService.RegisterAsync(
                    nameof(ReceiptProcessorController),
                    nameof(ProcessReceipt),
                    LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, new ReceiptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Errore interno: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Elabora un file caricato tramite POST multipart/form-data
        /// </summary>
        /// <param name="request">Richiesta con file attachment caricato</param>
        /// <returns>Risultato dell'estrazione</returns>
        [HttpPost("process")]
        [ProducesResponseType(typeof(ReceiptExtractionResult), 200)]
        public async Task<ActionResult<ReceiptExtractionResult>> ProcessReceiptPost(
            [FromBody] ProcessReceiptRequest request)
        {
            try
            {
                if (request == null || request.AttachmentFileId <= 0)
                {
                    return BadRequest(new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "ID file non valido"
                    });
                }

                _logger.LogInformation(
                    "Richiesta elaborazione receipt POST per file {FileId}",
                    request.AttachmentFileId);

                var result = await _receiptProcessor.ProcessReceiptAsync(
                    request.AttachmentFileId,
                    request.UseCustomModel,
                    request.CustomModelId);

                if (result.Success)
                {
                    await _logEventService.RegisterAsync(
                        nameof(ReceiptProcessorController),
                        "ProcessReceiptPost",
                        LogEvent.EventsTypes.Info,
                        $"Receipt POST elaborato: File={request.AttachmentFileId}, Totale={result.TotalAmount}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante ProcessReceiptPost");
                
                await _logEventService.RegisterAsync(
                    nameof(ReceiptProcessorController),
                    "ProcessReceiptPost",
                    LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, new ReceiptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Errore interno: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Verifica la configurazione e connettività con Azure Form Recognizer
        /// </summary>
        /// <returns>Stato del servizio</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<ActionResult<object>> HealthCheck()
        {
            try
            {
                var isHealthy = await _receiptProcessor.HealthCheckAsync();

                return Ok(new
                {
                    healthy = isHealthy,
                    service = "Azure Form Recognizer",
                    timestamp = DateTime.UtcNow,
                    message = isHealthy
                        ? "Servizio configurato e raggiungibile"
                        : "Servizio non configurato o non raggiungibile. Verifica appsettings.json"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante health check");
                
                return Ok(new
                {
                    healthy = false,
                    service = "Azure Form Recognizer",
                    timestamp = DateTime.UtcNow,
                    message = $"Errore: {ex.Message}"
                });
            }
        }
    }
}
