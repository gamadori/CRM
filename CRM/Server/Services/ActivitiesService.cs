using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Registro attivita' (timeline + follow-up) collegate polimorficamente ad
    /// Azienda/Contatto/Deal/Ticket. Fase 1: timeline e gestione attivita'.
    /// </summary>
    public class ActivitiesService : IActivitiesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public ActivitiesService(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<List<ActivityDTO>?> GetByEntityAsync(ActivityEntityType entityType, int entityId)
        {
            try
            {
                var currentUser = await SafeCurrentUserAsync();

                var items = await _context.Activities
                    .Include(a => a.User)
                    .Include(a => a.Assignee)
                    .Include(a => a.CompletedBy)
                    .Include(a => a.Participants)
                        .ThenInclude(p => p.User)
                    .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                    .OrderByDescending(a => a.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync();

                var dtos = items.Select(a =>
                {
                    var dto = a.ToDTO()!;
                    dto.Permits = ComputePermits(a.IdUser, a.IdAssignee, currentUser);
                    return dto;
                }).ToList();

                await ResolveEmailEngagementAsync(dtos);

                return dtos;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesService), nameof(GetByEntityAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>Popola la sintesi di engagement (stato/aperture/click) per le attività email con email collegata.</summary>
        private async Task ResolveEmailEngagementAsync(List<ActivityDTO> dtos)
        {
            var emailIds = dtos.Where(d => d.IdEmailSent != null).Select(d => d.IdEmailSent!.Value).Distinct().ToList();
            if (emailIds.Count == 0)
                return;

            var emails = await _context.EmailsSent
                .AsNoTracking()
                .Where(e => emailIds.Contains(e.Id))
                .Select(e => new { e.Id, e.EngagementStatus, e.OpenCount, e.ClickCount })
                .ToListAsync();

            var map = emails.ToDictionary(e => e.Id);

            foreach (var d in dtos)
            {
                if (d.IdEmailSent != null && map.TryGetValue(d.IdEmailSent.Value, out var e))
                {
                    d.EmailEngagement = e.EngagementStatus;
                    d.EmailOpenCount = e.OpenCount;
                    d.EmailClickCount = e.ClickCount;
                }
            }
        }

        public async Task<List<ActivityDTO>?> GetMyAgendaAsync(ActivityFilter? args)
        {
            try
            {
                var me = await SafeCurrentUserAsync();

                var q = _context.Activities
                    .Include(a => a.User)
                    .Include(a => a.Assignee)
                    .Include(a => a.CompletedBy)
                    .Include(a => a.Participants)
                        .ThenInclude(p => p.User)
                    .Where(a => a.DueDate != null);

                // Agenda personale: attività assegnate all'utente corrente
                if (!string.IsNullOrEmpty(me))
                    q = q.Where(a => a.IdAssignee == me);

                q = args?.State != null
                    ? q.Where(a => a.State == args.State)
                    : q.Where(a => a.State == ActivityState.Planned);

                if (args?.DateFrom != null)
                    q = q.Where(a => a.DueDate >= args.DateFrom);
                if (args?.DateTo != null)
                    q = q.Where(a => a.DueDate <= args.DateTo);
                if (args?.Kind != null)
                    q = q.Where(a => a.Kind == args.Kind);

                var items = await q.OrderBy(a => a.DueDate).AsNoTracking().ToListAsync();

                var dtos = items.Select(a => a.ToDTO()!).ToList();
                await ResolveEntityNamesAsync(dtos);
                foreach (var d in dtos)
                    d.Permits = ComputePermits(d.IdUser, d.IdAssignee, me);

                return dtos;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesService), nameof(GetMyAgendaAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>Risolve in batch il nome dell'entità collegata (azienda/contatto/deal/ticket).</summary>
        private async Task ResolveEntityNamesAsync(List<ActivityDTO> dtos)
        {
            if (dtos.Count == 0) return;

            int[] IdsOf(ActivityEntityType t) => dtos.Where(d => d.EntityType == t).Select(d => d.EntityId).Distinct().ToArray();

            var companyIds = IdsOf(ActivityEntityType.Company);
            var contactIds = IdsOf(ActivityEntityType.Contact);
            var leadIds = IdsOf(ActivityEntityType.Lead);
            var dealIds = IdsOf(ActivityEntityType.Deal);
            var ticketIds = IdsOf(ActivityEntityType.Ticket);

            var companies = await _context.Companies.Where(c => companyIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.RagioneSociale }).ToDictionaryAsync(x => x.Id, x => x.Name);
            var contacts = await _context.Contacts.Where(c => contactIds.Contains(c.Id))
                .Select(c => new { c.Id, Name = c.Name + " " + c.Surname }).ToDictionaryAsync(x => x.Id, x => x.Name);
            var leads = await _context.Leads.Where(l => leadIds.Contains(l.Id))
                .Select(l => new { l.Id, l.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
            var deals = await _context.Deals.Where(d => dealIds.Contains(d.Id))
                .Select(d => new { d.Id, d.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
            var tickets = await _context.Tickets.Where(t => ticketIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Numero }).ToDictionaryAsync(x => x.Id, x => x.Numero);

            foreach (var d in dtos)
            {
                d.EntityName = d.EntityType switch
                {
                    ActivityEntityType.Company => companies.GetValueOrDefault(d.EntityId, string.Empty),
                    ActivityEntityType.Contact => contacts.GetValueOrDefault(d.EntityId, string.Empty),
                    ActivityEntityType.Lead => leads.GetValueOrDefault(d.EntityId, string.Empty),
                    ActivityEntityType.Deal => deals.GetValueOrDefault(d.EntityId, string.Empty),
                    ActivityEntityType.Ticket => tickets.GetValueOrDefault(d.EntityId, string.Empty) ?? string.Empty,
                    _ => string.Empty
                };
            }
        }

        public async Task<ActivityDTO?> GetItemAsync(int id)
        {
            var item = await _context.Activities
                .Include(a => a.User)
                .Include(a => a.Assignee)
                .Include(a => a.CompletedBy)
                .Include(a => a.Participants)
                    .ThenInclude(p => p.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            var dto = item.ToDTO();
            if (dto != null)
                dto.Permits = ComputePermits(item!.IdUser, item.IdAssignee, await SafeCurrentUserAsync());
            return dto;
        }

        public async Task<APIResponseMessage<ActivityDTO>> PostAsync(Activity item)
        {
            try
            {
                if (item.Id > 0)
                {
                    var existing = await _context.Activities
                        .Include(a => a.Participants)
                        .FirstOrDefaultAsync(a => a.Id == item.Id);
                    if (existing == null)
                        return Fail("Attivita' non trovata", System.Net.HttpStatusCode.NotFound);

                    existing.Kind = item.Kind;
                    existing.Subject = item.Subject;
                    existing.Description = item.Description;
                    existing.EntityType = item.EntityType;
                    existing.EntityId = item.EntityId;
                    existing.IdAssignee = string.IsNullOrWhiteSpace(item.IdAssignee) ? existing.IdUser : item.IdAssignee;
                    existing.DueDate = item.DueDate;
                    // Se il promemoria viene spostato, ne va rischedulata la consegna da zero.
                    if (existing.ReminderAt != item.ReminderAt)
                    {
                        existing.ReminderStatus = ReminderStatus.Pending;
                        existing.ReminderRetryCount = 0;
                        existing.ReminderLastAttemptAt = null;
                        existing.ReminderLastError = null;
                    }
                    existing.ReminderAt = item.ReminderAt;
                    existing.State = item.State;
                    existing.DoneDate = item.State == ActivityState.Done ? (item.DoneDate ?? existing.DoneDate ?? DateTime.Now) : null;
                    existing.Outcome = item.State == ActivityState.Done ? item.Outcome : null;
                    existing.CompletionNotes = item.State == ActivityState.Done ? item.CompletionNotes : null;
                    existing.NextStep = item.State == ActivityState.Done ? item.NextStep : null;
                    existing.IdCompletedBy = item.State == ActivityState.Done
                        ? (item.IdCompletedBy ?? existing.IdCompletedBy ?? await SafeCurrentUserAsync())
                        : null;

                    SyncParticipants(existing, item.Participants, await SafeCurrentUserAsync());
                }
                else
                {
                    var currentUserId = await SafeCurrentUserAsync();

                    if (item.CreatedAt == default)
                        item.CreatedAt = DateTime.Now;
                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = currentUserId;
                    if (string.IsNullOrWhiteSpace(item.IdUser))
                        item.IdUser = null;
                    // Non assegnata esplicitamente -> assegnata al creatore
                    if (string.IsNullOrWhiteSpace(item.IdAssignee))
                        item.IdAssignee = item.IdUser;
                    if (item.State == ActivityState.Done && item.DoneDate == null)
                        item.DoneDate = DateTime.Now;
                    if (item.State == ActivityState.Done && string.IsNullOrWhiteSpace(item.IdCompletedBy))
                        item.IdCompletedBy = currentUserId;

                    foreach (var participant in item.Participants)
                    {
                        participant.AddedAt = DateTime.Now;
                        participant.AddedBy = currentUserId;
                    }

                    _context.Activities.Add(item);
                }

                await _context.SaveChangesAsync();
                return Ok(await GetItemAsync(item.Id), "Attivita' salvata");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio dell'attivita'", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<ActivityDTO>> CompleteAsync(int id, ActivityCompletionRequest? completion = null)
        {
            try
            {
                var item = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
                if (item == null)
                    return Fail("Attivita' non trovata", System.Net.HttpStatusCode.NotFound);

                var currentUserId = await SafeCurrentUserAsync();

                if (completion?.CreateFollowUp == true)
                {
                    if (string.IsNullOrWhiteSpace(completion.FollowUpSubject))
                        return Fail("Inserisci l'oggetto del follow-up", System.Net.HttpStatusCode.BadRequest);

                    if (completion.FollowUpDueDate == null)
                        return Fail("Inserisci la scadenza del follow-up", System.Net.HttpStatusCode.BadRequest);
                }

                item.State = ActivityState.Done;
                item.DoneDate = DateTime.Now;
                item.Outcome = Clean(completion?.Outcome);
                item.CompletionNotes = Clean(completion?.CompletionNotes);
                item.NextStep = Clean(completion?.NextStep);
                item.IdCompletedBy = currentUserId;

                if (completion?.CreateFollowUp == true)
                {
                    var followUp = new Activity
                    {
                        Kind = completion.FollowUpKind,
                        Subject = Clean(completion.FollowUpSubject)!,
                        Description = FollowUpDescription(item, completion),
                        EntityType = item.EntityType,
                        EntityId = item.EntityId,
                        IdUser = currentUserId,
                        IdAssignee = Clean(completion.FollowUpAssigneeId) ?? item.IdAssignee ?? currentUserId,
                        DueDate = completion.FollowUpDueDate,
                        State = ActivityState.Planned,
                        CreatedAt = DateTime.Now
                    };

                    _context.Activities.Add(followUp);
                }

                await _context.SaveChangesAsync();

                return Ok(await GetItemAsync(id), "Attivita' completata");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesService), nameof(CompleteAsync), EventsTypes.Error, ex);
                return Fail("Errore nel completamento dell'attivita'", System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Activities.FindAsync(id);
            if (item == null)
                return false;
            try
            {
                _context.Activities.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ActivitiesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private async Task<string?> SafeCurrentUserAsync()
        {
            try { return await _permitsService.IdUser(); }
            catch { return null; }
        }

        /// <summary>Read sempre; edit/delete al creatore o all'assegnatario (o utente non identificabile).</summary>
        private static int ComputePermits(string? ownerId, string? assigneeId, string? currentUserId)
        {
            int permits = PermitsHelper.SetRead(0);
            permits = PermitsHelper.SetInsert(permits);
            if (string.IsNullOrEmpty(currentUserId) || ownerId == currentUserId || assigneeId == currentUserId
                || (string.IsNullOrEmpty(ownerId) && string.IsNullOrEmpty(assigneeId)))
            {
                permits = PermitsHelper.SetEdit(permits);
                permits = PermitsHelper.SetDelete(permits);
            }
            return permits;
        }

        private static APIResponseMessage<ActivityDTO> Fail(string msg, System.Net.HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static APIResponseMessage<ActivityDTO> Ok(ActivityDTO? data, string msg)
            => new() { State = true, Data = data, Message = msg, Code = System.Net.HttpStatusCode.OK };

        private static string? Clean(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static void SyncParticipants(Activity activity, IEnumerable<ActivityParticipant>? participants, string? currentUserId)
        {
            var requested = participants?
                .Select(p => p.IdUser)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToHashSet()
                ?? new HashSet<string>();

            var existing = activity.Participants.Select(p => p.IdUser).ToHashSet();

            foreach (var participant in activity.Participants.Where(p => !requested.Contains(p.IdUser)).ToList())
                activity.Participants.Remove(participant);

            foreach (var idUser in requested.Where(id => !existing.Contains(id)))
            {
                activity.Participants.Add(new ActivityParticipant
                {
                    IdUser = idUser,
                    AddedAt = DateTime.Now,
                    AddedBy = currentUserId
                });
            }
        }

        private static string? FollowUpDescription(Activity source, ActivityCompletionRequest completion)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(completion.NextStep))
                parts.Add(completion.NextStep.Trim());

            parts.Add($"Follow-up generato dalla chiusura dell'attivita' #{source.Id}: {source.Subject}");

            if (!string.IsNullOrWhiteSpace(completion.Outcome))
                parts.Add($"Esito: {completion.Outcome.Trim()}");

            return string.Join(Environment.NewLine, parts);
        }
    }
}
