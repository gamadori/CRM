using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Net;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Fasi operative di una commessa (WBS/Gantt): gerarchia, dipendenze vincolanti, avanzamento
    /// derivato dai ticket e percorso critico. Accesso mediato dal perimetro aziende della commessa.
    /// </summary>
    public class CommessaFasiService : ICommessaFasiService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public CommessaFasiService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<List<CommessaFaseDTO>?> GetTreeAsync(int idCommessa)
        {
            try
            {
                if (!await CanAccessCommessaAsync(idCommessa))
                    return new List<CommessaFaseDTO>();

                var fasi = await _context.CommessaFasi
                    .Include(f => f.Dependencies)
                    .Include(f => f.Tickets)
                    .Include(f => f.TicketPlans)
                        .ThenInclude(p => p.Ticket)
                    .Include(f => f.TicketPlans)
                    .Include(f => f.UserTakenBy)
                    .Include(f => f.TicketType)
                    .Include(f => f.Group)
                    .Where(f => f.IdCommessa == idCommessa)
                    .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                var dtos = fasi.Select(f => f.ToDTO()).ToList();
                ComputeCriticalPath(dtos);

                // Chi puo' prendere in carico decide il server: il client si limita a mostrarlo.
                var (unrestricted, myGroupIds) = await GetTakeContextAsync();
                foreach (var d in dtos)
                    d.CanTake = unrestricted || d.IdGroup == null || myGroupIds.Contains(d.IdGroup.Value);

                return dtos;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(GetTreeAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        /// <summary>
        /// Singola fase con il codice della commessa: serve al ticket, che conosce solo
        /// l'id della fase e deve mostrare a quale commessa appartiene.
        /// </summary>
        public async Task<CommessaFaseDTO?> GetItemAsync(int faseId)
        {
            try
            {
                var fase = await _context.CommessaFasi
                    .Include(f => f.Commessa)
                    .Include(f => f.TicketPlans)
                    .Include(f => f.TicketType)
                    .Include(f => f.Group)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == faseId);

                if (fase == null || !await CanAccessCommessaAsync(fase.IdCommessa))
                    return null;

                var dto = fase.ToDTO();
                dto.CommessaCode = fase.Commessa?.Code ?? string.Empty;
                return dto;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(GetItemAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto)
        {
            try
            {
                if (!await CanAccessCommessaAsync(dto.IdCommessa))
                    return Fail("Commessa non accessibile", HttpStatusCode.Forbidden);

                Normalize(dto);

                CommessaFase entity;
                bool wasDone = false;
                if (dto.Id > 0)
                {
                    entity = await FaseWithTicketWorkAsync(dto.Id);
                    if (entity == null || entity.IdCommessa != dto.IdCommessa)
                        return Fail("Fase non trovata", HttpStatusCode.NotFound);

                    // Modificare una fase equivale a lavorarci: vale la stessa regola di gruppo
                    // della presa in carico, altrimenti si completa la fase di un altro reparto
                    // chiamando l'API a mano.
                    if (!await CanActOnGroupAsync(entity.IdGroup))
                        return Fail("Non sei abilitato a modificare questa fase", HttpStatusCode.Forbidden);

                    // Spostare la fase a un altro gruppo segue la regola della creazione: solo verso
                    // un gruppo di cui si fa parte, altrimenti si assegnerebbe lavoro a un reparto
                    // qualsiasi uscendo dal proprio perimetro.
                    if (dto.IdGroup != entity.IdGroup && !await CanActOnGroupAsync(dto.IdGroup))
                        return Fail("Non sei abilitato ad assegnare la fase a questo gruppo", HttpStatusCode.Forbidden);

                    // Chiudere a mano una fase con predecessori aperti equivale a saltare la coda.
                    // Si blocca solo l'avanzamento: ripianificare date e anagrafica resta libero.
                    if (dto.Progress > 0 || dto.State != CommessaFaseStates.Pending)
                    {
                        var blockers = await GetStartBlockersAsync(entity.Id);
                        if (blockers.Count > 0)
                            return Fail(BlockedMessage(blockers), HttpStatusCode.Conflict);
                    }

                    // "Richiede ticket": la fase non si dichiara conclusa senza un ticket chiuso che
                    // ne documenti il lavoro. Finora il flag era salvato, mostrato e mai letto.
                    if (dto.Progress >= 100 && entity.RequiresTicket && !HasClosedTicket(entity))
                        return Fail("La fase richiede almeno un ticket chiuso per essere completata", HttpStatusCode.Conflict);

                    if (dto.Progress >= 100 && HasOpenBlockedTicket(entity))
                        return Fail("La fase contiene ticket bloccati: risolvi i blocchi prima di completarla", HttpStatusCode.Conflict);

                    entity.Name = dto.Name;
                    entity.Description = dto.Description;
                    entity.ParentId = dto.ParentId;
                    entity.StartDate = dto.StartDate;
                    entity.EndDate = dto.EndDate;
                    entity.SortOrder = dto.SortOrder;
                    entity.IsMilestone = dto.IsMilestone;
                    entity.Color = dto.Color;
                    entity.CompletionMode = dto.CompletionMode;
                    entity.AutoCreateTicketOnTake = dto.AutoCreateTicketOnTake;
                    entity.RequiresTicket = dto.RequiresTicket;

                    // Gruppo e tipo ticket ora si modificano dall'editor delle fasi: senza, una fase
                    // nata senza tipo (commessa aperta, fase aggiunta a mano) non avrebbe mai potuto
                    // aprire ticket, e non c'era modo di rimediare. Chi chiama SaveAsync deve quindi
                    // mandare il DTO completo: entrambi gli editor ricopiano i valori esistenti.
                    entity.IdGroup = dto.IdGroup;
                    entity.IdTicketType = dto.IdTicketType;

                    wasDone = entity.State == CommessaFaseStates.Done;
                    entity.Progress = Math.Clamp(dto.Progress, 0, 100);
                    ApplyStateAndProgress(entity);
                }
                else
                {
                    if (!await CanActOnGroupAsync(dto.IdGroup))
                        return Fail("Non sei abilitato a creare fasi per questo gruppo", HttpStatusCode.Forbidden);

                    entity = dto.ToEntity();
                    entity.Progress = Math.Clamp(dto.Progress, 0, 100);
                    ApplyStateAndProgress(entity);
                    if (entity.SortOrder == 0)
                        entity.SortOrder = await NextSortOrderAsync(dto.IdCommessa);
                    _context.CommessaFasi.Add(entity);
                }

                await _context.SaveChangesAsync();

                // Completamento manuale: vale come chiusura, quindi sblocca le fasi successive.
                if (!wasDone && entity.State == CommessaFaseStates.Done)
                    await GenerateStartTicketsForSuccessorsAsync(entity.Id);

                await CascadeSuccessorDatesAsync(entity.IdCommessa, new List<int> { entity.Id });
                // Dopo la cascata: la fase salvata e i successori trascinati hanno date nuove e
                // i loro ticket aperti devono scadere con la fase, non alla data di ieri.
                await ProductionTicketDeadlines.SyncCommessaAsync(_context, entity.IdCommessa);
                await RecomputeRollupFromAsync(entity.Id);
                await RecomputeCommessaProgressAsync(dto.IdCommessa);

                return new APIResponseMessage<CommessaFaseDTO>
                {
                    State = true,
                    Data = entity.ToDTO(),
                    Message = "Fase salvata",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(SaveAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio della fase", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos)
        {
            try
            {
                if (dtos == null || dtos.Count == 0)
                    return true;

                var idCommessa = dtos[0].IdCommessa;
                if (!await CanAccessCommessaAsync(idCommessa))
                    return false;

                var ids = dtos.Select(d => d.Id).ToList();
                var entities = await _context.CommessaFasi
                    .Include(f => f.Tickets)
                    .Include(f => f.TicketPlans)
                        .ThenInclude(p => p.Ticket)
                    .Where(f => f.IdCommessa == idCommessa && ids.Contains(f.Id))
                    .ToListAsync();

                // Stessa regola di gruppo del salvataggio singolo: senza questa, riprogrammare
                // in blocco sarebbe la scorciatoia per modificare le fasi di un altro reparto.
                var (unrestricted, myGroupIds) = await GetTakeContextAsync();
                if (!unrestricted && entities.Any(e => e.IdGroup != null && !myGroupIds.Contains(e.IdGroup.Value)))
                    return false;

                var map = entities.ToDictionary(e => e.Id);
                foreach (var dto in dtos)
                {
                    if (!map.TryGetValue(dto.Id, out var e))
                        continue;
                    Normalize(dto);
                    e.StartDate = dto.StartDate;
                    e.EndDate = dto.EndDate;
                    e.SortOrder = dto.SortOrder;
                    e.ParentId = dto.ParentId;
                    e.IsMilestone = dto.IsMilestone;
                    e.CompletionMode = dto.CompletionMode;
                    e.AutoCreateTicketOnTake = dto.AutoCreateTicketOnTake;
                    e.RequiresTicket = dto.RequiresTicket;

                    e.Progress = Math.Clamp(dto.Progress, 0, 100);
                    if (e.Progress >= 100 && HasOpenBlockedTicket(e))
                        return false;
                    ApplyStateAndProgress(e);
                }

                await _context.SaveChangesAsync();
                await CascadeSuccessorDatesAsync(idCommessa, entities.Select(e => e.Id).ToList());
                await ProductionTicketDeadlines.SyncCommessaAsync(_context, idCommessa);
                foreach (var e in entities)
                    await RecomputeRollupFromAsync(e.Id);
                await RecomputeCommessaProgressAsync(idCommessa);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(BulkSaveAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int faseId)
        {
            try
            {
                var fase = await _context.CommessaFasi.FirstOrDefaultAsync(f => f.Id == faseId);
                if (fase == null || !await CanAccessCommessaAsync(fase.IdCommessa))
                    return false;

                var deps = await _context.CommessaFaseDependencies
                    .Where(d => d.IdFase == faseId || d.IdPredecessorFase == faseId)
                    .ToListAsync();
                _context.CommessaFaseDependencies.RemoveRange(deps);

                var children = await _context.CommessaFasi.Where(f => f.ParentId == faseId).ToListAsync();
                foreach (var c in children)
                    c.ParentId = fase.ParentId;

                var tickets = await _context.Tickets.Where(t => t.IdCommessaFase == faseId).ToListAsync();
                foreach (var t in tickets)
                    t.IdCommessaFase = null;

                _context.CommessaFasi.Remove(fase);
                await _context.SaveChangesAsync();
                await RecomputeCommessaProgressAsync(fase.IdCommessa);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto)
        {
            try
            {
                var fase = await _context.CommessaFasi.FirstOrDefaultAsync(f => f.Id == dto.IdFase);
                var pred = await _context.CommessaFasi.FirstOrDefaultAsync(f => f.Id == dto.IdPredecessorFase);
                if (fase == null || pred == null)
                    return new() { State = false, Message = "Fase non trovata", Code = HttpStatusCode.NotFound };

                if (fase.IdCommessa != pred.IdCommessa || !await CanAccessCommessaAsync(fase.IdCommessa))
                    return new() { State = false, Message = "Fasi non valide", Code = HttpStatusCode.Forbidden };

                if (dto.IdFase == dto.IdPredecessorFase)
                    return new() { State = false, Message = "Una fase non puo' dipendere da se stessa", Code = HttpStatusCode.BadRequest };

                var already = await _context.CommessaFaseDependencies
                    .AnyAsync(d => d.IdFase == dto.IdFase && d.IdPredecessorFase == dto.IdPredecessorFase);
                if (already)
                    return new() { State = false, Message = "Dipendenza gia' presente", Code = HttpStatusCode.Conflict };

                if (await WouldCreateCycleAsync(fase.IdCommessa, dto.IdFase, dto.IdPredecessorFase))
                    return new() { State = false, Message = "La dipendenza creerebbe un ciclo", Code = HttpStatusCode.Conflict };

                var entity = new CommessaFaseDependency
                {
                    IdFase = dto.IdFase,
                    IdPredecessorFase = dto.IdPredecessorFase,
                    LagDays = dto.LagDays,
                    Type = dto.Type
                };
                _context.CommessaFaseDependencies.Add(entity);
                await _context.SaveChangesAsync();

                return new() { State = true, Data = entity.ToDTO(), Message = "Dipendenza creata", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(AddDependencyAsync), EventsTypes.Error, ex);
                return new() { State = false, Message = "Errore nella creazione della dipendenza", Code = HttpStatusCode.InternalServerError };
            }
        }

        public async Task<bool> RemoveDependencyAsync(int dependencyId)
        {
            try
            {
                var dep = await _context.CommessaFaseDependencies
                    .Include(d => d.Fase)
                    .FirstOrDefaultAsync(d => d.Id == dependencyId);
                if (dep == null || dep.Fase == null || !await CanAccessCommessaAsync(dep.Fase.IdCommessa))
                    return false;

                _context.CommessaFaseDependencies.Remove(dep);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(RemoveDependencyAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task RecomputeFaseProgressAsync(int? faseId)
        {
            if (faseId == null) return;
            try
            {
                var fase = await FaseWithTicketWorkAsync(faseId.Value);
                if (fase == null) return;

                var wasDone = fase.State == CommessaFaseStates.Done;
                ApplyStateAndProgress(fase);
                await _context.SaveChangesAsync();

                // Appena la fase si chiude, le successive che non hanno piu' vincoli aperti
                // ricevono i ticket previsti "a inizio fase".
                if (!wasDone && fase.State == CommessaFaseStates.Done)
                    await GenerateStartTicketsForSuccessorsAsync(fase.Id);
                // Parte dalla fase stessa: se ha figli e' un raggruppamento e il valore appena
                // calcolato dai suoi ticket va sostituito dalla sintesi dei figli.
                await RecomputeRollupFromAsync(fase.Id);
                await RecomputeCommessaProgressAsync(fase.IdCommessa);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(RecomputeFaseProgressAsync), EventsTypes.Error, ex);
            }
        }

        // ─── Presa in carico ─────────────────────────────────────────────────────

        /// <summary>
        /// Contesto per decidere chi puo' prendere in carico una fase.
        /// Admin e SuperUser non sono vincolati; Standard e Client devono appartenere al gruppo
        /// abilitato della fase. Una fase senza gruppo non pone vincoli di appartenenza.
        /// </summary>
        private async Task<(bool unrestricted, HashSet<int> myGroupIds)> GetTakeContextAsync()
        {
            if (await _permitsService.IsAdmin() || await _permitsService.IsSuperUser())
                return (true, new HashSet<int>());

            var idUser = await _permitsService.IdUser();
            if (string.IsNullOrEmpty(idUser))
                return (false, new HashSet<int>());

            var groupIds = await _context.Groups
                .AsNoTracking()
                .Where(g => g.Users.Any(u => u.Id == idUser))
                .Select(g => g.Id)
                .ToListAsync();

            return (false, groupIds.ToHashSet());
        }

        public async Task<bool> CanTakeFaseAsync(int faseId)
        {
            var idGroup = await _context.CommessaFasi
                .AsNoTracking()
                .Where(f => f.Id == faseId)
                .Select(f => f.IdGroup)
                .FirstOrDefaultAsync();

            return await CanActOnGroupAsync(idGroup);
        }

        /// <summary>
        /// Predecessori che impediscono l'avvio della fase. Le dipendenze sono vincolanti, ma il
        /// vincolo riguarda l'AVVIO: una fase gia' in lavorazione non viene ricongelata se un
        /// predecessore torna aperto (es. ticket riaperto), altrimenti si bloccherebbe lavoro in corso.
        /// Lista vuota = fase avviabile. Stessa regola applicata dalla UI (predecessore = Done),
        /// indipendente dal <see cref="DependencyType"/>: oggi e' implementato solo Finish-to-Start.
        /// </summary>
        public async Task<List<string>> GetStartBlockersAsync(int faseId)
        {
            var state = await _context.CommessaFasi
                .AsNoTracking()
                .Where(f => f.Id == faseId)
                .Select(f => (CommessaFaseStates?)f.State)
                .FirstOrDefaultAsync();

            if (state == null || state != CommessaFaseStates.Pending)
                return new List<string>();

            return await _context.CommessaFaseDependencies
                .AsNoTracking()
                .Where(d => d.IdFase == faseId
                    && d.PredecessorFase != null
                    && d.PredecessorFase.State != CommessaFaseStates.Done)
                .Select(d => d.PredecessorFase!.Name)
                .ToListAsync();
        }

        private static string BlockedMessage(List<string> blockers)
            => $"Fasi precedenti non completate: {string.Join(", ", blockers)}";

        /// <summary>
        /// Regola unica di abilitazione sulle fasi: Admin e SuperUser non sono vincolati, gli altri
        /// devono appartenere al gruppo abilitato. Una fase senza gruppo non pone vincoli.
        /// </summary>
        private async Task<bool> CanActOnGroupAsync(int? idGroup)
        {
            var (unrestricted, myGroupIds) = await GetTakeContextAsync();
            return unrestricted || idGroup == null || myGroupIds.Contains(idGroup.Value);
        }

        public async Task<APIResponseMessage<CommessaFaseTicketPlanDTO>> GenerateTicketFromPlanAsync(int ticketPlanId)
        {
            try
            {
                var plan = await _context.CommessaFaseTicketPlans
                    .Include(p => p.CommessaFase)
                        .ThenInclude(f => f!.Commessa)
                    .FirstOrDefaultAsync(p => p.Id == ticketPlanId);

                if (plan?.CommessaFase?.Commessa == null)
                    return new() { State = false, Message = "Piano ticket non trovato", Code = HttpStatusCode.NotFound };

                var fase = plan.CommessaFase;
                var commessa = fase.Commessa;

                if (!await CanAccessCommessaAsync(fase.IdCommessa))
                    return new() { State = false, Message = "Commessa non accessibile", Code = HttpStatusCode.Forbidden };

                if (plan.IdTicket != null)
                    return new() { State = true, Data = plan.ToDTO(), Message = "Ticket gia' generato", Code = HttpStatusCode.OK };

                if (!await CanTakeFaseAsync(fase.Id))
                    return new() { State = false, Message = "Non sei abilitato a generare ticket per questa fase", Code = HttpStatusCode.Forbidden };

                var blockers = await GetStartBlockersAsync(fase.Id);
                if (blockers.Count > 0)
                    return new() { State = false, Message = BlockedMessage(blockers), Code = HttpStatusCode.Conflict };

                await CreateTicketFromPlanAsync(plan, fase, commessa, await _permitsService.IdUser());
                await RecomputeFaseProgressAsync(fase.Id);

                return new()
                {
                    State = true,
                    Data = plan.ToDTO(),
                    Message = "Ticket generato",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(GenerateTicketFromPlanAsync), EventsTypes.Error, ex);
                return new() { State = false, Message = "Errore nella generazione del ticket", Code = HttpStatusCode.InternalServerError };
            }
        }

        /// <summary>
        /// Creazione effettiva del ticket dal piano, senza controlli: e' separata perche' la
        /// generazione automatica non ha un utente "abilitato" da verificare, e' il sistema che
        /// esegue il template. I controlli restano a carico dei chiamanti pubblici.
        /// </summary>
        private async Task CreateTicketFromPlanAsync(CommessaFaseTicketPlan plan, CommessaFase fase, Commessa commessa, string idUserOpened)
        {
            var description = string.IsNullOrWhiteSpace(plan.Description)
                ? $"{commessa.Code} - {fase.Name} - {plan.Title}"
                : plan.Description;

            var ticket = new Ticket
            {
                IdType = plan.IdTicketType,
                IdCompany = commessa.IdCompany ?? 0,
                IdProduct = commessa.IdProduct,
                IdArticle = commessa.IdArticle,
                IdCommessaFase = fase.Id,
                IdGroupAssigned = plan.IdGroupAssigned,
                IdUserOpened = idUserOpened,
                DateOpened = DateTime.Now,
                Date = DateTime.Today,
                // Fine e scadenza sono quelle della fase, non il calcolo SLA per tipo ticket.
                DateEnd = fase.EndDate.Date,
                DateExpired = fase.EndDate.Date,
                Description = description,
                Numero = string.Empty,
                CloseDescription = string.Empty,
                CloseNote = string.Empty,
                Priority = commessa.Priority,
                Support = (int)TypesSupport.Office,
                Payment = (int)Payments.Free
            };

            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            plan.IdTicket = ticket.Id;
            if (fase.State == CommessaFaseStates.Pending)
            {
                fase.State = CommessaFaseStates.InProgress;
                fase.TakenAt ??= DateTime.Now;
                fase.IdUserTakenBy ??= idUserOpened;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Chiusa una fase, le successive diventano avviabili: i loro piani ticket marcati
        /// "a inizio fase" vengono generati subito. Finora quella modalita' si comportava come
        /// Manual, perche' nessuno la eseguiva mai. Idempotente: i piani gia' generati si saltano.
        /// </summary>
        private async Task GenerateStartTicketsForSuccessorsAsync(int faseId)
        {
            var successorIds = await _context.CommessaFaseDependencies
                .AsNoTracking()
                .Where(d => d.IdPredecessorFase == faseId)
                .Select(d => d.IdFase)
                .Distinct()
                .ToListAsync();

            foreach (var successorId in successorIds)
            {
                if ((await GetStartBlockersAsync(successorId)).Count > 0)
                    continue;

                var plans = await _context.CommessaFaseTicketPlans
                    .Include(p => p.CommessaFase)
                        .ThenInclude(f => f!.Commessa)
                    .Where(p => p.IdCommessaFase == successorId
                        && p.AutoCreateMode == ProductionTicketAutoCreateMode.OnPhaseStart
                        && p.IdTicket == null)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .ToListAsync();

                if (plans.Count == 0)
                    continue;

                var idUser = await _permitsService.IdUser();
                foreach (var plan in plans)
                {
                    if (plan.CommessaFase?.Commessa == null)
                        continue;
                    await CreateTicketFromPlanAsync(plan, plan.CommessaFase, plan.CommessaFase.Commessa, idUser);
                }
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────────

        private static APIResponseMessage<CommessaFaseDTO> Fail(string msg, HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };

        private static void Normalize(CommessaFaseDTO dto)
        {
            dto.Name = (dto.Name ?? string.Empty).Trim();
            if (dto.IsMilestone)
                dto.EndDate = dto.StartDate;
            else if (dto.EndDate < dto.StartDate)
                dto.EndDate = dto.StartDate;
        }

        private async Task<int> NextSortOrderAsync(int idCommessa)
        {
            var max = await _context.CommessaFasi
                .Where(f => f.IdCommessa == idCommessa)
                .Select(f => (int?)f.SortOrder)
                .MaxAsync() ?? 0;
            return max + 1;
        }

        /// <summary>
        /// Fase con tutto ciò che serve a derivarne stato e avanzamento: senza i piani ticket
        /// il calcolo ricadrebbe sui soli ticket collegati, ignorando i previsti non ancora generati.
        /// </summary>
        private Task<CommessaFase?> FaseWithTicketWorkAsync(int faseId)
            => _context.CommessaFasi
                .Include(f => f.Tickets)
                .Include(f => f.TicketPlans)
                    .ThenInclude(p => p.Ticket)
                .FirstOrDefaultAsync(f => f.Id == faseId);

        /// <summary>
        /// Allinea stato e avanzamento alla regola di completamento della fase. Non li si prende
        /// mai dal client: sono una conseguenza dei ticket o della percentuale manuale, altrimenti
        /// un DTO incompleto riporterebbe a Pending una fase già avviata.
        /// </summary>
        internal static void ApplyStateAndProgress(CommessaFase fase)
        {
            var (closed, total) = TicketProgressNumbers(fase);

            // Niente lavoro tracciato a ticket: comanda la percentuale.
            if (UsesManualProgress(fase) || total == 0)
            {
                ApplyManualProgress(fase);
                return;
            }

            fase.Progress = TicketDrivenProgress(fase.CompletionMode, closed, total);
            if (fase.Progress >= 100)
            {
                fase.State = CommessaFaseStates.Done;
            }
            else if (HasGeneratedTickets(fase))
            {
                // È il ticket aperto a dichiarare avviata la fase: i soli previsti non bastano.
                fase.State = CommessaFaseStates.InProgress;
            }
            else
            {
                fase.State = CommessaFaseStates.Pending;
            }

            if (fase.State != CommessaFaseStates.Pending)
            {
                fase.TakenAt ??= DateTime.Now;
                fase.IdUserTakenBy ??= FirstTicketOwner(fase);
            }
        }

        /// <summary>
        /// Chi ha preso in carico la fase: l'assegnatario del primo ticket collegato, o in mancanza
        /// chi lo ha aperto. Il campo esisteva fin dall'inizio ma nessuno lo valorizzava.
        /// </summary>
        private static string? FirstTicketOwner(CommessaFase fase)
        {
            var first = fase.Tickets?
                .OrderBy(t => t.DateOpened)
                .ThenBy(t => t.Id)
                .FirstOrDefault();

            if (first == null)
                return null;

            return string.IsNullOrWhiteSpace(first.IdUserAssigned) ? first.IdUserOpened : first.IdUserAssigned;
        }

        private static bool HasClosedTicket(CommessaFase fase)
            => (fase.Tickets?.Any(t => t.Closed) ?? false)
                || (fase.TicketPlans?.Any(p => p.Ticket != null && p.Ticket.Closed) ?? false);

        private static bool HasOpenBlockedTicket(CommessaFase fase)
            => (fase.Tickets?.Any(t => t.IsBlocked && !t.Closed) ?? false)
                || (fase.TicketPlans?.Any(p => p.Ticket != null && p.Ticket.IsBlocked && !p.Ticket.Closed) ?? false);

        private static bool HasGeneratedTickets(CommessaFase fase)
            => (fase.Tickets?.Count ?? 0) > 0
                || (fase.TicketPlans?.Any(p => p.IdTicket != null) ?? false);

        private static bool UsesManualProgress(CommessaFase fase)
            => fase.CompletionMode is CommessaFaseCompletionMode.Manual or CommessaFaseCompletionMode.ProgressManual;

        private static void ApplyManualProgress(CommessaFase fase)
        {
            fase.Progress = Math.Clamp(fase.Progress, 0, 100);
            fase.State = fase.Progress >= 100
                ? CommessaFaseStates.Done
                : fase.Progress > 0
                    ? CommessaFaseStates.InProgress
                    : CommessaFaseStates.Pending;
        }

        private static int TicketDrivenProgress(CommessaFaseCompletionMode mode, int closed, int total)
        {
            if (total <= 0)
                return 0;

            return mode == CommessaFaseCompletionMode.AnyTicketClosed
                ? (closed > 0 ? 100 : 0)
                : (int)Math.Round(closed * 100.0 / total);
        }

        private static (int closed, int total) TicketProgressNumbers(CommessaFase fase)
        {
            var requiredPlans = fase.TicketPlans?
                .Where(p => p.Required)
                .ToList() ?? new List<CommessaFaseTicketPlan>();

            if (requiredPlans.Count > 0)
                return (requiredPlans.Count(p => p.IdTicket != null && p.Ticket?.Closed == true), requiredPlans.Count);

            var tickets = fase.Tickets?.ToList() ?? new List<Ticket>();
            return (tickets.Count(t => t.Closed), tickets.Count);
        }

        private async Task<bool> CanAccessCommessaAsync(int idCommessa)
        {
            var idCompany = await _context.Commesse
                .Where(c => c.Id == idCommessa)
                .Select(c => c.IdCompany)
                .FirstOrDefaultAsync();

            var allowed = await _permitsService.GetVisibleCompanyIds();
            if (allowed == null)
                return true;
            return idCompany != null && allowed.Contains(idCompany.Value);
        }

        /// <summary>
        /// Propaga in avanti il vincolo di sequenza: spostata una fase, i successori che
        /// inizierebbero prima della fine del predecessore slittano, a catena. La durata in giorni
        /// lavorativi viene conservata.
        /// Solo in avanti: il sistema non anticipa mai il lavoro, perche' una data pianificata puo'
        /// dipendere da vincoli che non conosce (materiali, disponibilita' del reparto).
        /// </summary>
        private async Task CascadeSuccessorDatesAsync(int idCommessa, List<int> movedIds)
        {
            if (movedIds.Count == 0)
                return;

            var fasi = await _context.CommessaFasi
                .Where(f => f.IdCommessa == idCommessa)
                .ToListAsync();
            if (fasi.Count == 0)
                return;

            var byId = fasi.ToDictionary(f => f.Id);
            var deps = await _context.CommessaFaseDependencies
                .AsNoTracking()
                .Where(d => d.Fase!.IdCommessa == idCommessa)
                .Select(d => new { d.IdFase, d.IdPredecessorFase, d.LagDays })
                .ToListAsync();

            if (CascadeDates(fasi, deps.Select(d => (d.IdFase, d.IdPredecessorFase, d.LagDays)).ToList(), movedIds))
                await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Algoritmo di propagazione, separato dall'accesso al database per poterlo verificare:
        /// sposta in avanti i successori che violano il vincolo e restituisce true se ha cambiato
        /// qualcosa. Modifica le entita' in memoria, non salva.
        /// </summary>
        internal static bool CascadeDates(
            List<CommessaFase> fasi,
            List<(int IdFase, int IdPredecessorFase, int LagDays)> deps,
            List<int> movedIds)
        {
            var byId = fasi.ToDictionary(f => f.Id);
            var successors = deps
                .GroupBy(d => d.IdPredecessorFase)
                .ToDictionary(g => g.Key, g => g.ToList());

            var queue = new Queue<int>(movedIds);
            // Il grafo e' aciclico (AddDependencyAsync lo garantisce), ma i dati sono vecchi:
            // il contatore evita un ciclo infinito se un ciclo fosse gia' finito in tabella.
            int guard = 0, maxSteps = fasi.Count * 10 + 50;
            bool changed = false;

            while (queue.Count > 0 && guard++ < maxSteps)
            {
                var currentId = queue.Dequeue();
                if (!byId.TryGetValue(currentId, out var current)) continue;
                if (!successors.TryGetValue(currentId, out var links)) continue;

                foreach (var link in links)
                {
                    if (!byId.TryGetValue(link.IdFase, out var succ)) continue;

                    // Finish-to-Start: il successore non puo' iniziare prima del primo giorno
                    // lavorativo dopo la fine del predecessore, piu' l'eventuale lag.
                    var earliest = current.EndDate.Date.AddWorkdays(1 + Math.Max(0, link.LagDays));
                    if (succ.StartDate.Date >= earliest)
                        continue;

                    int duration = succ.StartDate.CountWorkdays(succ.EndDate);
                    succ.StartDate = earliest;
                    succ.EndDate = succ.IsMilestone
                        ? earliest
                        : earliest.AddWorkdays(Math.Max(1, duration) - 1);

                    changed = true;
                    queue.Enqueue(succ.Id);
                }
            }

            return changed;
        }

        /// <summary>Stato di un raggruppamento: concluso se tutti i figli lo sono, avviato se almeno uno lo è.</summary>
        internal static CommessaFaseStates RollupState(IEnumerable<CommessaFaseStates> childStates)
        {
            var states = childStates.ToList();
            if (states.Count == 0)
                return CommessaFaseStates.Pending;

            return states.All(s => s == CommessaFaseStates.Done)
                ? CommessaFaseStates.Done
                : states.Any(s => s != CommessaFaseStates.Pending)
                    ? CommessaFaseStates.InProgress
                    : CommessaFaseStates.Pending;
        }

        /// <summary>Media pesata sulla durata lavorativa, milestone escluse (durata 0, peserebbero comunque 1).</summary>
        internal static int WeightedProgress(List<(DateTime Start, DateTime End, int Progress, bool IsMilestone)> items)
        {
            var counted = items.Where(i => !i.IsMilestone).ToList();
            if (counted.Count == 0)
                return 0;

            double weightSum = 0, acc = 0;
            foreach (var i in counted)
            {
                double w = Math.Max(1, i.Start.CountWorkdays(i.End));
                weightSum += w;
                acc += w * Math.Clamp(i.Progress, 0, 100);
            }
            return weightSum > 0 ? (int)Math.Round(acc / weightSum) : 0;
        }

        /// <summary>
        /// Ricalcola il nodo indicato come raggruppamento WBS (se ha figli) e risale fino alla radice.
        /// Una fase con figli non ha un avanzamento proprio: e' la sintesi dei figli. Prima ne aveva
        /// uno indipendente, che restava a 0 finche' qualcuno non la chiudeva a mano.
        /// </summary>
        private async Task RecomputeRollupFromAsync(int? nodeId)
        {
            int guard = 0;
            while (nodeId != null && guard++ < 50)
            {
                var node = await _context.CommessaFasi.FirstOrDefaultAsync(f => f.Id == nodeId.Value);
                if (node == null)
                    return;

                var children = await _context.CommessaFasi
                    .Where(f => f.ParentId == node.Id)
                    .Select(f => new { f.StartDate, f.EndDate, f.Progress, f.IsMilestone, f.State })
                    .ToListAsync();

                if (children.Count > 0)
                {
                    var wasDone = node.State == CommessaFaseStates.Done;

                    node.Progress = WeightedProgress(children
                        .Select(c => (c.StartDate, c.EndDate, c.Progress, c.IsMilestone))
                        .ToList());

                    node.State = RollupState(children.Select(c => c.State));

                    await _context.SaveChangesAsync();

                    // Anche un raggruppamento che si chiude e' un predecessore completato.
                    if (!wasDone && node.State == CommessaFaseStates.Done)
                        await GenerateStartTicketsForSuccessorsAsync(node.Id);
                }

                nodeId = node.ParentId;
            }
        }

        /// <summary>
        /// Avanzamento commessa = media pesata sui giorni delle sole fasi foglia (milestone escluse).
        /// Le fasi padre sono raggruppamenti e il loro avanzamento e' gia' la sintesi dei figli:
        /// contarle peserebbe due volte lo stesso lavoro, dato che il loro span copre quello dei figli.
        /// </summary>
        private async Task RecomputeCommessaProgressAsync(int idCommessa)
        {
            var fasi = await _context.CommessaFasi
                .Where(f => f.IdCommessa == idCommessa)
                .Select(f => new
                {
                    f.StartDate,
                    f.EndDate,
                    f.Progress,
                    f.IsMilestone,
                    HasChildren = _context.CommessaFasi.Any(c => c.ParentId == f.Id)
                })
                .ToListAsync();

            var counted = fasi.Where(f => !f.IsMilestone && !f.HasChildren).ToList();
            int progress = WeightedProgress(counted
                .Select(f => (f.StartDate, f.EndDate, f.Progress, f.IsMilestone))
                .ToList());

            var commessa = await _context.Commesse.FirstOrDefaultAsync(c => c.Id == idCommessa);
            if (commessa == null) return;

            // Stati terminali/manuali: non toccare avanzamento né stato.
            if (commessa.State is CommessaStates.Delivered or CommessaStates.Cancelled)
                return;

            commessa.Progress = progress;

            // Avanzamento automatico dello stato solo per gli stati "guidati dalle fasi".
            // Suspended/Testing restano manuali. Completed può retrocedere se un ticket viene riaperto.
            if (commessa.State is CommessaStates.Planned or CommessaStates.InProgress or CommessaStates.Completed)
            {
                if (progress >= 100 && counted.Count > 0)
                {
                    commessa.State = CommessaStates.Completed;
                    commessa.EndDateActual ??= DateTime.Now;
                }
                else if (progress > 0)
                {
                    commessa.State = CommessaStates.InProgress;
                    commessa.StartDateActual ??= DateTime.Now;
                }
                else
                {
                    commessa.State = CommessaStates.Planned;
                }
            }

            await _context.SaveChangesAsync();

            // Cascata alla riga d'ordine (produzione).
            await SyncOrderRowStatusAsync(commessa.IdOrderRow);
        }

        /// <summary>Ricalcola lo stato di produzione della riga d'ordine in base alle sue commesse.</summary>
        private async Task SyncOrderRowStatusAsync(int? orderRowId)
        {
            if (orderRowId == null) return;
            var row = await _context.OrderRows.FirstOrDefaultAsync(r => r.Id == orderRowId.Value);
            if (row == null) return;

            var states = await _context.Commesse.Where(c => c.IdOrderRow == orderRowId).Select(c => c.State).ToListAsync();
            if (states.Count == 0)
                row.ProductionStatus = RowProductionStatus.None;
            else if (states.All(s => s is CommessaStates.Delivered or CommessaStates.Completed or CommessaStates.Cancelled))
                row.ProductionStatus = RowProductionStatus.Closed;
            else
                row.ProductionStatus = RowProductionStatus.InProduction;

            await _context.SaveChangesAsync();
        }

        private async Task<bool> WouldCreateCycleAsync(int idCommessa, int idFase, int idPredecessor)
        {
            var deps = await _context.CommessaFaseDependencies
                .Where(d => d.Fase!.IdCommessa == idCommessa)
                .Select(d => new { d.IdFase, d.IdPredecessorFase })
                .ToListAsync();

            var succ = deps.GroupBy(d => d.IdPredecessorFase)
                           .ToDictionary(g => g.Key, g => g.Select(x => x.IdFase).ToList());

            var stack = new Stack<int>();
            stack.Push(idFase);
            var seen = new HashSet<int>();
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == idPredecessor)
                    return true;
                if (!seen.Add(cur))
                    continue;
                if (succ.TryGetValue(cur, out var next))
                    foreach (var n in next)
                        stack.Push(n);
            }
            return false;
        }

        /// <summary>Percorso critico (CPM su durate + dipendenze FS con lag): imposta IsCriticalPath sui DTO.</summary>
        internal static void ComputeCriticalPath(List<CommessaFaseDTO> fasi)
        {
            if (fasi.Count == 0) return;

            var byId = fasi.ToDictionary(f => f.Id);
            var preds = fasi.ToDictionary(f => f.Id, f => new List<(int pred, int lag)>());
            var succs = fasi.ToDictionary(f => f.Id, f => new List<(int succ, int lag)>());

            foreach (var f in fasi)
                foreach (var d in f.Dependencies)
                    if (byId.ContainsKey(d.IdPredecessorFase))
                    {
                        preds[f.Id].Add((d.IdPredecessorFase, d.LagDays));
                        succs[d.IdPredecessorFase].Add((f.Id, d.LagDays));
                    }

            var indeg = fasi.ToDictionary(f => f.Id, f => preds[f.Id].Count);
            var queue = new Queue<int>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var order = new List<int>();
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                order.Add(id);
                foreach (var (s, _) in succs[id])
                    if (--indeg[s] == 0)
                        queue.Enqueue(s);
            }
            if (order.Count != fasi.Count)
                return;

            double Dur(CommessaFaseDTO f) => f.IsMilestone ? 0 : Math.Max(1, f.StartDate.CountWorkdays(f.EndDate));

            var es = new Dictionary<int, double>();
            var ef = new Dictionary<int, double>();
            foreach (var id in order)
            {
                var f = byId[id];
                double start = 0;
                foreach (var (p, lag) in preds[id])
                    start = Math.Max(start, ef[p] + lag);
                es[id] = start;
                ef[id] = start + Dur(f);
            }

            double projectFinish = ef.Values.DefaultIfEmpty(0).Max();

            var lf = new Dictionary<int, double>();
            var ls = new Dictionary<int, double>();
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var id = order[i];
                var f = byId[id];
                double finish = succs[id].Count == 0
                    ? projectFinish
                    : succs[id].Min(x => ls[x.succ] - x.lag);
                lf[id] = finish;
                ls[id] = finish - Dur(f);
            }

            foreach (var f in fasi)
                f.IsCriticalPath = ls[f.Id] - es[f.Id] <= 0.0001;
        }
    }
}
