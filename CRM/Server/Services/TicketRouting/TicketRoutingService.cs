using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services.TicketRouting
{
    /// <summary>
    /// Regole dello smistamento automatico. La chiamata al modello sta dietro
    /// <see cref="ITicketRoutingAiClient"/>: qui restano soglia, candidati, ripiego e tracciamento
    /// dell'esito, cioe' la parte che deve restare prevedibile e verificabile senza rete.
    /// </summary>
    public class TicketRoutingService : ITicketRoutingService
    {
        /// <summary>
        /// Lo smistamento avviene dentro la chiamata che crea il ticket: oltre questo tempo si
        /// rinuncia e il ticket resta in coda, invece di far aspettare chi ha premuto "Salva".
        /// </summary>
        private static readonly TimeSpan AiTimeout = TimeSpan.FromSeconds(30);

        private readonly ApplicationDbContext _context;
        private readonly ITicketRoutingAiClient _ai;
        private readonly ITicketsService _ticketsService;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ILogger<TicketRoutingService> _logger;

        public TicketRoutingService(
            ApplicationDbContext context,
            ITicketRoutingAiClient ai,
            ITicketsService ticketsService,
            IPermitsService permitsService,
            ILogEventService logEventService,
            ILogger<TicketRoutingService> logger)
        {
            _context = context;
            _ai = ai;
            _ticketsService = ticketsService;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _logger = logger;
        }

        public async Task<TicketRoutingResult> RouteAsync(int idTicket, TicketRoutingSource source, CancellationToken ct = default)
        {
            try
            {
                var settings = await GetSettingsAsync(ct);

                if (!settings.Enabled)
                    return TicketRoutingResult.NotExecuted;

                if (source == TicketRoutingSource.InboundEmail && !settings.ApplyToEmailTickets)
                    return TicketRoutingResult.NotExecuted;

                var ticket = await _context.Tickets
                    .Include(t => t.TicketType)
                    .Include(t => t.Product)
                    .Include(t => t.Company)
                    .FirstOrDefaultAsync(t => t.Id == idTicket, ct);

                if (ticket == null)
                    return TicketRoutingResult.NotExecuted;

                // Un gruppo gia' scelto (fase di commessa, creazione manuale) non si tocca.
                if (ticket.IdGroupAssigned != null)
                    return TicketRoutingResult.NotExecuted;

                var candidates = await GetCandidatesAsync(ticket.IdType, settings.RestrictToTicketTypeGroups, ct);

                var suggestion = candidates.Count > 0
                    ? await CallAiAsync(BuildRequest(ticket, candidates), settings.Model, ct)
                    : null;

                ticket.AiRoutedAt = DateTime.Now;

                if (suggestion?.GroupId == null)
                    return await ApplyNoSuggestionAsync(ticket, settings, candidates.Count, suggestion, ct);

                ticket.AiSuggestedGroupId = suggestion.GroupId;
                ticket.AiRoutingConfidence = suggestion.Confidence;
                ticket.AiRoutingReason = Truncate(suggestion.Reason, 2000);

                if (suggestion.Confidence >= settings.AutoAssignThreshold)
                {
                    ticket.IdGroupAssigned = suggestion.GroupId;
                    ticket.AiRoutingApplied = true;
                    ticket.AiRoutingOutcome = AiRoutingOutcome.Accepted;
                    ticket.IdState = await GetStateIdAsync(eTicketStates.Assigned, ticket.IdState, ct);
                }
                else
                {
                    // Sotto soglia il ticket resta visibile nella coda generale: un errore dell'AI
                    // non deve mai far sparire un ticket dentro il gruppo sbagliato.
                    ticket.AiRoutingApplied = false;
                    ticket.AiRoutingOutcome = AiRoutingOutcome.Pending;
                }

                await _context.SaveChangesAsync(ct);

                return new TicketRoutingResult(
                    Executed: true,
                    AssignedGroupId: ticket.IdGroupAssigned,
                    SuggestedGroupId: ticket.AiSuggestedGroupId,
                    Confidence: ticket.AiRoutingConfidence,
                    Outcome: ticket.AiRoutingOutcome);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketRoutingService), nameof(RouteAsync), EventsTypes.Error, ex);
                return TicketRoutingResult.NotExecuted;
            }
        }

        public async Task<TicketRoutingPreviewResult> PreviewAsync(TicketRoutingPreviewRequest request, CancellationToken ct = default)
        {
            var settings = await GetSettingsAsync(ct);
            var candidates = await GetCandidatesAsync(request.IdTicketType, settings.RestrictToTicketTypeGroups, ct);

            var result = new TicketRoutingPreviewResult
            {
                Candidates = candidates.Select(c => c.Name).ToList()
            };

            if (!_ai.IsAvailable)
            {
                result.Error = "Chiave API non configurata: lo smistamento AI non e' operativo.";
                return result;
            }

            if (candidates.Count == 0)
            {
                result.Error = settings.RestrictToTicketTypeGroups
                    ? "Nessun gruppo abilitato per questo tipo di ticket: collega almeno un gruppo al tipo, oppure disattiva il filtro."
                    : "Nessun gruppo configurato.";
                return result;
            }

            var ticketType = await _context.TicketTypes
                .AsNoTracking()
                .Where(t => t.Id == request.IdTicketType)
                .Select(t => t.Desc)
                .FirstOrDefaultAsync(ct);

            var suggestion = await CallAiAsync(
                new TicketRoutingRequest(ticketType ?? string.Empty, request.Description, null, null, candidates),
                settings.Model,
                ct);

            if (suggestion == null)
            {
                result.Error = "Il modello non ha restituito una risposta utilizzabile.";
                return result;
            }

            result.Confidence = suggestion.Confidence;
            result.Reason = suggestion.Reason;

            if (suggestion.GroupId != null)
            {
                result.IdGroup = suggestion.GroupId;
                result.GroupName = candidates.FirstOrDefault(c => c.Id == suggestion.GroupId)?.Name;
                result.WouldAutoAssign = suggestion.Confidence >= settings.AutoAssignThreshold;
            }

            return result;
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> AcceptSuggestionAsync(int idTicket, CancellationToken ct = default)
        {
            try
            {
                var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == idTicket, ct);

                if (ticket == null)
                    return Fail("Ticket non trovato", System.Net.HttpStatusCode.NotFound);

                if (!await _permitsService.CanGetObject(ticket.IdCompany))
                    return Fail("Ticket non accessibile", System.Net.HttpStatusCode.Forbidden);

                if (ticket.AiSuggestedGroupId == null)
                    return Fail("Nessun suggerimento da accettare", System.Net.HttpStatusCode.BadRequest);

                if (ticket.Closed)
                    return Fail("Un ticket chiuso non puo' essere riassegnato", System.Net.HttpStatusCode.BadRequest);

                ticket.IdGroupAssigned = ticket.AiSuggestedGroupId;
                ticket.AiRoutingOutcome = AiRoutingOutcome.Accepted;
                ticket.IdState = await GetStateIdAsync(eTicketStates.Assigned, ticket.IdState, ct);

                await _context.SaveChangesAsync(ct);

                return await OkAsync(idTicket, "Ticket assegnato al gruppo suggerito");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketRoutingService), nameof(AcceptSuggestionAsync), EventsTypes.Error, ex);
                return Fail("Errore nell'assegnazione del gruppo", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> DismissSuggestionAsync(int idTicket, CancellationToken ct = default)
        {
            try
            {
                var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == idTicket, ct);

                if (ticket == null)
                    return Fail("Ticket non trovato", System.Net.HttpStatusCode.NotFound);

                if (!await _permitsService.CanGetObject(ticket.IdCompany))
                    return Fail("Ticket non accessibile", System.Net.HttpStatusCode.Forbidden);

                if (ticket.AiSuggestedGroupId == null)
                    return Fail("Nessun suggerimento da scartare", System.Net.HttpStatusCode.BadRequest);

                // Il suggerimento resta registrato per le statistiche: cambia solo l'esito.
                ticket.AiRoutingOutcome = AiRoutingOutcome.Dismissed;
                await _context.SaveChangesAsync(ct);

                return await OkAsync(idTicket, "Suggerimento scartato");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketRoutingService), nameof(DismissSuggestionAsync), EventsTypes.Error, ex);
                return Fail("Errore nella gestione del suggerimento", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<TicketRoutingSetting> GetSettingsAsync(CancellationToken ct = default)
        {
            var settings = await _context.TicketRoutingSettings
                .Include(s => s.FallbackGroup)
                .FirstOrDefaultAsync(ct);

            if (settings != null)
                return settings;

            // Prima apertura: la riga viene creata disattivata, cosi' nulla cambia finche'
            // qualcuno non decide di accendere lo smistamento.
            settings = new TicketRoutingSetting { Id = TicketRoutingSetting.SingletonId };
            _context.TicketRoutingSettings.Add(settings);
            await _context.SaveChangesAsync(ct);

            return settings;
        }

        public async Task<TicketRoutingStatusDTO> GetStatusAsync(CancellationToken ct = default)
        {
            var since = DateTime.Now.AddDays(-30);
            var settings = await GetSettingsAsync(ct);

            var routed = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.AiRoutedAt != null && t.AiRoutedAt >= since)
                .Select(t => new { t.AiRoutingApplied, t.AiRoutingOutcome })
                .ToListAsync(ct);

            return new TicketRoutingStatusDTO
            {
                AiAvailable = _ai.IsAvailable,
                Model = string.IsNullOrWhiteSpace(settings.Model) ? _ai.Model : settings.Model!,
                GroupsTotal = await _context.Groups.CountAsync(ct),
                GroupsWithHints = await _context.Groups.CountAsync(g => g.AiRoutingHints != null && g.AiRoutingHints != "", ct),
                TicketTypesWithoutGroups = await _context.TicketTypes.CountAsync(t => !t.Groups.Any(), ct),
                RoutedLast30Days = routed.Count,
                AutoAssignedLast30Days = routed.Count(x => x.AiRoutingApplied),
                AcceptedLast30Days = routed.Count(x => x.AiRoutingOutcome == AiRoutingOutcome.Accepted),
                CorrectedLast30Days = routed.Count(x => x.AiRoutingOutcome == AiRoutingOutcome.Corrected),
                PendingLast30Days = routed.Count(x => x.AiRoutingOutcome == AiRoutingOutcome.Pending),
                DismissedLast30Days = routed.Count(x => x.AiRoutingOutcome == AiRoutingOutcome.Dismissed)
            };
        }

        // ─── Interni ────────────────────────────────────────────────────────────

        private async Task<List<TicketRoutingCandidate>> GetCandidatesAsync(int idTicketType, bool restrictToTicketTypeGroups, CancellationToken ct)
        {
            var groups = _context.Groups.AsNoTracking();

            // Pre-filtro deterministico: la mappa Tipo ticket -> Gruppi e' gia' la definizione di
            // "chi tratta cosa". Passarla al modello riduce i token e le scelte fantasiose.
            if (restrictToTicketTypeGroups)
                groups = groups.Where(g => g.TicketTypes.Any(t => t.Id == idTicketType));

            return await groups
                .OrderBy(g => g.Name)
                .Select(g => new TicketRoutingCandidate(g.Id, g.Name, g.Description, g.AiRoutingHints))
                .ToListAsync(ct);
        }

        private static TicketRoutingRequest BuildRequest(Ticket ticket, IReadOnlyList<TicketRoutingCandidate> candidates) =>
            new(
                TicketType: ticket.TicketType?.Desc ?? string.Empty,
                Description: ticket.Description ?? string.Empty,
                Company: ticket.Company?.RagioneSociale,
                Product: ticket.Product?.Name,
                Candidates: candidates);

        private async Task<TicketRoutingSuggestion?> CallAiAsync(TicketRoutingRequest request, string? model, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(AiTimeout);

            try
            {
                return await _ai.SuggestGroupAsync(request, model, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Smistamento AI interrotto dopo {Seconds}s: il ticket resta in coda", AiTimeout.TotalSeconds);
                return null;
            }
        }

        /// <summary>
        /// Nessun gruppo individuato (modello incerto, AI spenta o nessun candidato): se e'
        /// configurato un gruppo di ripiego il ticket ci finisce, altrimenti resta nella coda generale.
        /// </summary>
        private async Task<TicketRoutingResult> ApplyNoSuggestionAsync(
            Ticket ticket,
            TicketRoutingSetting settings,
            int candidateCount,
            TicketRoutingSuggestion? suggestion,
            CancellationToken ct)
        {
            ticket.AiSuggestedGroupId = null;
            ticket.AiRoutingConfidence = suggestion?.Confidence;
            ticket.AiRoutingApplied = false;
            ticket.AiRoutingOutcome = AiRoutingOutcome.None;
            ticket.AiRoutingReason = Truncate(
                suggestion?.Reason ?? (candidateCount == 0
                    ? "Nessun gruppo abilitato per questo tipo di ticket."
                    : "L'AI non ha individuato un gruppo competente."),
                2000);

            if (settings.IdFallbackGroup != null)
            {
                ticket.IdGroupAssigned = settings.IdFallbackGroup;
                ticket.IdState = await GetStateIdAsync(eTicketStates.Assigned, ticket.IdState, ct);
            }

            await _context.SaveChangesAsync(ct);

            return new TicketRoutingResult(
                Executed: true,
                AssignedGroupId: ticket.IdGroupAssigned,
                SuggestedGroupId: null,
                Confidence: ticket.AiRoutingConfidence,
                Outcome: AiRoutingOutcome.None);
        }

        private async Task<int?> GetStateIdAsync(eTicketStates state, int? current, CancellationToken ct) =>
            await _context.TicketStates
                .Where(s => s.State == (int)state)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct) ?? current;

        private async Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> OkAsync(int idTicket, string message) =>
            new()
            {
                State = true,
                Code = System.Net.HttpStatusCode.OK,
                Message = message,
                Data = await _ticketsService.GetDetailsAsync(idTicket)
            };

        private static CRM.Client.Models.APIResponseMessage<TicketDTO> Fail(string message, System.Net.HttpStatusCode code) =>
            new() { State = false, Code = code, Message = message };

        private static string? Truncate(string? value, int maxLength) =>
            string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
