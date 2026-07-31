using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Controller per la gestione delle note spese (ExpenseReceipts)
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseReceiptsController : ControllerBase
    {
        private readonly IExpenseReceiptService _expenseReceiptService;
        private readonly CRM.Client.Services.ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public ExpenseReceiptsController(
            IExpenseReceiptService expenseReceiptService,
            CRM.Client.Services.ILogEventService logEventService,
            IPermitsService permits)
        {
            _expenseReceiptService = expenseReceiptService;
            _logEventService = logEventService;
            _permits = permits;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        /// <summary>
        /// Le note spese sono un dato personale: ognuno vede le proprie, admin e superuser tutte.
        /// Restituisce l'id a cui restringere la lettura, oppure null per chi vede tutto.
        /// </summary>
        private async Task<string> GetVisibilityRestrictionAsync()
        {
            if (await _permits.IsAdmin() || await _permits.IsSuperUser())
                return null;

            return GetUserId();
        }

        /// <summary>
        /// Ottiene tutte le note spese di un intervento
        /// </summary>
        /// <summary>
        /// Elenco trasversale filtrato. E' l'endpoint che alimenta la pagina globale.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> Search([FromQuery] ExpenseReceiptFilter filter)
        {
            try
            {
                var restrictTo = await GetVisibilityRestrictionAsync();
                var (items, total) = await _expenseReceiptService.SearchAsync(filter, restrictTo);

                return Ok(new { items, totalCount = total, canSeeAll = restrictTo == null });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController), nameof(Search),
                    CRM.Shared.LogEvent.EventsTypes.Error, ex);

                return StatusCode(500, "Errore durante la ricerca delle note spese");
            }
        }

        /// <summary>
        /// Totali sull'insieme filtrato: e' il "conto della serva" vero e proprio.
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ExpenseReceiptSummaryDTO>> GetGlobalSummary([FromQuery] ExpenseReceiptFilter filter)
        {
            try
            {
                var restrictTo = await GetVisibilityRestrictionAsync();
                return Ok(await _expenseReceiptService.GetSummaryAsync(filter, restrictTo));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController), nameof(GetGlobalSummary),
                    CRM.Shared.LogEvent.EventsTypes.Error, ex);

                return StatusCode(500, "Errore durante il calcolo dei totali");
            }
        }

        /// <summary>
        /// Cambio di riferimento proposto per una valuta a una data. Il form lo precompila e
        /// lascia comunque modificare: per un rimborso vale spesso il cambio applicato dalla
        /// carta, che i cambi BCE non conoscono.
        /// </summary>
        [HttpGet("exchange-rate")]
        public async Task<ActionResult<decimal?>> GetExchangeRate(
            [FromServices] IExchangeRateService exchangeRates,
            [FromServices] CRM.Server.Data.ApplicationDbContext context,
            string currency,
            DateTime? date)
        {
            try
            {
                var baseCurrency = await context.GlobalSettings
                    .AsNoTracking()
                    .OrderBy(g => g.Id)
                    .Select(g => g.BaseCurrency)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(baseCurrency))
                    baseCurrency = "EUR";

                // Null e' una risposta valida: valuta non coperta o servizio non raggiungibile.
                // Il chiamante deve poter salvare lo stesso, lasciando la spesa da convertire.
                return Ok(await exchangeRates.GetRateAsync(currency, baseCurrency, date ?? DateTime.Today));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController), nameof(GetExchangeRate),
                    CRM.Shared.LogEvent.EventsTypes.Error, ex);

                return Ok((decimal?)null);
            }
        }

        [HttpGet("intervention/{interventionId}")]
        public async Task<ActionResult<object>> GetByIntervention(int interventionId)
        {
            try
            {
                var receipts = await _expenseReceiptService.GetByInterventionIdAsync(interventionId);
                var summary = await _expenseReceiptService.GetSummaryByInterventionIdAsync(interventionId);

                return Ok(new { receipts, summary });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(GetByIntervention),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante il recupero delle note spese");
            }
        }

        /// <summary>
        /// Ottiene il riepilogo delle note spese di un intervento
        /// </summary>
        [HttpGet("intervention/{interventionId}/summary")]
        public async Task<ActionResult<ExpenseReceiptSummaryDTO>> GetSummary(int interventionId)
        {
            try
            {
                var summary = await _expenseReceiptService.GetSummaryByInterventionIdAsync(interventionId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(GetSummary),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante il recupero del riepilogo");
            }
        }

        /// <summary>
        /// Note spese di un'attivita', con il riepilogo: e' quello che la pagina dell'attivita'
        /// mostra nella scheda Note spese.
        /// </summary>
        [HttpGet("activity/{activityId}")]
        public async Task<ActionResult<object>> GetByActivity(int activityId)
        {
            try
            {
                var receipts = await _expenseReceiptService.GetByActivityIdAsync(activityId);
                var summary = await _expenseReceiptService.GetSummaryByActivityIdAsync(activityId);

                return Ok(new { receipts, summary });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(GetByActivity),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante il recupero delle note spese");
            }
        }

        /// <summary>
        /// Solo il riepilogo di un'attivita': la scheda lo usa per il contatore sulla linguetta,
        /// senza tirarsi dietro l'elenco.
        /// </summary>
        [HttpGet("activity/{activityId}/summary")]
        public async Task<ActionResult<ExpenseReceiptSummaryDTO>> GetActivitySummary(int activityId)
        {
            try
            {
                return Ok(await _expenseReceiptService.GetSummaryByActivityIdAsync(activityId));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(GetActivitySummary),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante il recupero del riepilogo");
            }
        }

        /// <summary>
        /// Ottiene una singola nota spese per ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseReceiptDTO>> GetById(int id)
        {
            try
            {
                var receipt = await _expenseReceiptService.GetByIdAsync(id);
                if (receipt == null)
                    return NotFound();

                return Ok(receipt);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(GetById),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante il recupero della nota spese");
            }
        }

        /// <summary>
        /// Crea una nuova nota spese
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ExpenseReceiptDTO>> Create([FromBody] ExpenseReceiptCreateUpdateDTO dto)
        {
            try
            {
                var userId = GetUserId();
                var receipt = await _expenseReceiptService.CreateAsync(dto, userId);

                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Create),
                    CRM.Shared.LogEvent.EventsTypes.Info,
                    $"Creata nota spese #{receipt.Id} per intervento #{dto.TicketInterventionId}");

                return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Create),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante la creazione della nota spese");
            }
        }

        /// <summary>
        /// Crea una nota spesa autonoma per ogni documento fiscale rilevato nello stesso file.
        /// Tutte le note condividono allegato, persona e contesto.
        /// </summary>
        [HttpPost("batch")]
        public async Task<ActionResult<List<ExpenseReceiptDTO>>> CreateBatch(
            [FromBody] ExpenseReceiptCreateUpdateDTO dto)
        {
            try
            {
                var receipts = await _expenseReceiptService.CreateBatchAsync(dto, GetUserId());
                return Ok(receipts);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(CreateBatch),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);
                return StatusCode(500, "Errore durante la creazione automatica delle note spese");
            }
        }

        /// <summary>
        /// Aggiorna una nota spese esistente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ExpenseReceiptDTO>> Update(int id, [FromBody] ExpenseReceiptCreateUpdateDTO dto)
        {
            try
            {
                var userId = GetUserId();
                var receipt = await _expenseReceiptService.UpdateAsync(id, dto, userId);

                if (receipt == null)
                    return NotFound();

                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Update),
                    CRM.Shared.LogEvent.EventsTypes.Info,
                    $"Aggiornata nota spese #{id}");

                return Ok(receipt);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Update),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante l'aggiornamento della nota spese");
            }
        }

        /// <summary>
        /// Elimina una nota spese
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var result = await _expenseReceiptService.DeleteAsync(id);

                if (!result)
                    return NotFound();

                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Delete),
                    CRM.Shared.LogEvent.EventsTypes.Info,
                    $"Eliminata nota spese #{id}");

                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Delete),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante l'eliminazione della nota spese");
            }
        }

        /// <summary>
        /// Conferma i dati estratti di una nota spese
        /// </summary>
        [HttpPost("{id}/confirm")]
        public async Task<ActionResult> Confirm(int id)
        {
            try
            {
                var userId = GetUserId();
                var result = await _expenseReceiptService.ConfirmAsync(id, userId);

                if (!result)
                    return NotFound();

                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Confirm),
                    CRM.Shared.LogEvent.EventsTypes.Info,
                    $"Confermata nota spese #{id}");

                return Ok();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(Confirm),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante la conferma della nota spese");
            }
        }

        /// <summary>
        /// Crea una nota spese dal risultato dell'estrazione Azure
        /// </summary>
        [HttpPost("from-extraction")]
        public async Task<ActionResult<ExpenseReceiptDTO>> CreateFromExtraction([FromBody] CreateFromExtractionRequest request)
        {
            try
            {
                var userId = GetUserId();
                var receipt = await _expenseReceiptService.CreateFromExtractionAsync(
                    request.InterventionId,
                    request.AttachmentFileId,
                    request.ExtractionResult,
                    userId);

                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(CreateFromExtraction),
                    CRM.Shared.LogEvent.EventsTypes.Info,
                    $"Creata nota spese #{receipt.Id} da estrazione Azure per intervento #{request.InterventionId}");

                return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(ExpenseReceiptsController),
                    nameof(CreateFromExtraction),
                    CRM.Shared.LogEvent.EventsTypes.Error,
                    ex);

                return StatusCode(500, "Errore durante la creazione della nota spese da estrazione");
            }
        }
    }

    public class CreateFromExtractionRequest
    {
        public int InterventionId { get; set; }
        public int AttachmentFileId { get; set; }
        public ReceiptExtractionResult ExtractionResult { get; set; }
    }
}
