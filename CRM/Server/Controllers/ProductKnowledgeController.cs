using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Gestione della base di conoscenza (voci associate ai modelli/prodotti).
    /// Lettura per gli utenti autenticati; scrittura riservata agli amministratori.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductKnowledgeController : ControllerBase
    {
        private readonly IKnowledgeService _knowledge;
        private readonly ILogEventService _logEventService;

        public ProductKnowledgeController(IKnowledgeService knowledge, ILogEventService logEventService)
        {
            _knowledge = knowledge;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProductKnowledgeDTO>>> GetList([FromQuery] ProductKnowledgeFilter filter)
        {
            try
            {
                return await _knowledge.GetListAsync(filter);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(GetList), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante il recupero della base di conoscenza: {ex.Message}");
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<ProductKnowledgeDTO>> Get(int id)
        {
            var item = await _knowledge.GetItemAsync(id);
            return item == null ? NotFound() : item;
        }

        [HttpPost]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<ProductKnowledgeDTO>> Save([FromBody] ProductKnowledge item)
        {
            try
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Content))
                    return BadRequest("Titolo e contenuto sono obbligatori");

                return await _knowledge.SaveAsync(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(Save), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante il salvataggio: {ex.Message}");
            }
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = "AdminRole")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _knowledge.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        /// <summary>Elimina un intero documento importato (tutte le sue parti) in un colpo solo.</summary>
        [HttpDelete("document/{groupId:guid}")]
        [Authorize(Policy = "AdminRole")]
        public async Task<IActionResult> DeleteDocument(Guid groupId)
        {
            try
            {
                var removed = await _knowledge.DeleteDocumentAsync(groupId);
                return removed > 0 ? Ok(new { removed }) : NotFound();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(DeleteDocument), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante l'eliminazione del documento: {ex.Message}");
            }
        }

        /// <summary>Rigenera l'embedding di tutte le parti di un documento importato.</summary>
        [HttpPost("document/{groupId:guid}/generate-embeddings")]
        [Authorize(Policy = "AdminRole")]
        public async Task<IActionResult> RegenerateDocumentEmbeddings(Guid groupId)
        {
            try
            {
                var processed = await _knowledge.RegenerateDocumentEmbeddingsAsync(groupId);
                return Ok(new { processed });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(RegenerateDocumentEmbeddings), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante la rigenerazione degli embedding del documento: {ex.Message}");
            }
        }

        [HttpPost("generate-embeddings")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<KnowledgeEmbeddingStats>> GenerateEmbeddings([FromQuery] int batchSize = 20)
        {
            try
            {
                return await _knowledge.GenerateMissingEmbeddingsAsync(batchSize);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(GenerateEmbeddings), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante la generazione degli embedding: {ex.Message}");
            }
        }

        /// <summary>Importa un documento (PDF/Word/testo) generando le voci di conoscenza.</summary>
        [HttpPost("import-document")]
        [Authorize(Policy = "AdminRole")]
        [RequestSizeLimit(52_428_800)] // 50 MB
        public async Task<ActionResult<KnowledgeImportResult>> ImportDocument(
            [FromForm] IFormFile file, [FromForm] int? idProduct, [FromForm] string? category,
            [FromForm] int? pageFrom, [FromForm] int? pageTo)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("File mancante o vuoto");

                if (!DocumentTextExtractor.IsSupported(file.FileName))
                    return BadRequest("Formato non supportato. Ammessi: PDF, DOCX, TXT, MD.");

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                return await _knowledge.ImportDocumentAsync(ms.ToArray(), file.FileName, idProduct, category, pageFrom, pageTo);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductKnowledgeController), nameof(ImportDocument), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante l'import del documento: {ex.Message}");
            }
        }
    }
}
