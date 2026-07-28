using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Net;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Commesse di produzione (MTO). Una commessa per unità, generata dalla riga d'ordine clonando
    /// il template di fasi del prodotto con schedulazione all'indietro dalla consegna.
    /// Perimetro aziende fail-closed.
    /// </summary>
    public class CommesseService : ICommesseService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public CommesseService(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<CommessaDTO?> GetItemAsync(int id)
        {
            var item = await _context.Commesse
                .Include(c => c.Company)
                .Include(c => c.Product)
                .Include(c => c.Article)
                .Include(c => c.UserResponsible)
                .Include(c => c.OrderRow).ThenInclude(r => r!.Order)
                .Include(c => c.Phases)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (item == null || !await CanAccessAsync(item.IdCompany))
                return null;

            var dto = item.ToDTO()!;
            dto.TicketCount = await _context.Tickets.CountAsync(t => t.CommessaFase!.IdCommessa == id);
            dto.BlockedTicketCount = await _context.Tickets.CountAsync(t => t.CommessaFase!.IdCommessa == id && !t.Closed && t.IsBlocked);
            dto.Permits = await _permitsService.ObjectPermits(dto.IdCompany, dto.IdUserResponsible ?? string.Empty);
            return dto;
        }

        public async Task<PagingResponse<CommessaDTO, int>?> GetSummaryAsync(CommessaFilter? args)
        {
            try
            {
                var items = await FilterItems(args);
                if (items == null) return new();

                int count = await items.CountAsync();
                if (args?.Skip != null && args.Top != null)
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);

                var list = await items
                    .Select(c => new CommessaDTO
                    {
                        Id = c.Id,
                        Code = c.Code,
                        IdOrderRow = c.IdOrderRow,
                        IdOrder = c.OrderRow != null ? c.OrderRow.IdOrder : (int?)null,
                        OrderNumber = c.OrderRow != null && c.OrderRow.Order != null ? (c.OrderRow.Order.Number ?? string.Empty) : string.Empty,
                        IdCompany = c.IdCompany,
                        CompanyName = c.Company != null ? c.Company.RagioneSociale : string.Empty,
                        IdProduct = c.IdProduct,
                        ProductName = c.Product != null ? c.Product.Name : string.Empty,
                        IdArticle = c.IdArticle,
                        ArticleSerial = c.Article != null ? c.Article.SerialNumber : string.Empty,
                        Name = c.Name,
                        State = c.State,
                        Priority = c.Priority,
                        StartDatePlanned = c.StartDatePlanned,
                        EndDatePlanned = c.EndDatePlanned,
                        ExpectedEndDate = c.Phases.Any() ? c.Phases.Max(f => f.EndDate) : c.EndDatePlanned,
                        StartDateActual = c.StartDateActual,
                        EndDateActual = c.EndDateActual,
                        Progress = c.Progress,
                        BudgetHours = c.BudgetHours,
                        IdUserResponsible = c.IdUserResponsible,
                        ResponsibleName = c.UserResponsible != null ? c.UserResponsible.NameComplete : string.Empty,
                        CreatedAt = c.CreatedAt,
                        PhaseCount = c.Phases.Count,
                        TicketCount = c.Phases.SelectMany(f => f.Tickets).Count(),
                        BlockedTicketCount = c.Phases.SelectMany(f => f.Tickets).Count(t => !t.Closed && t.IsBlocked)
                    })
                    .ToListAsync();

                foreach (var c in list)
                    c.Permits = await _permitsService.ObjectPermits(c.IdCompany, c.IdUserResponsible ?? string.Empty);

                return new PagingResponse<CommessaDTO, int>
                {
                    Items = list,
                    MetaData = new PagingHeaderModel { TotalCount = count, PageSize = args != null ? args.PageSize : 0 },
                    Total = count
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(GetSummaryAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<CommessaDTO>?> GetListAsync(CommessaFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);
                if (items == null) return new List<CommessaDTO>();
                return await items.Select(c => new CommessaDTO
                {
                    Id = c.Id,
                    Code = c.Code,
                    IdOrderRow = c.IdOrderRow,
                    IdOrder = c.OrderRow != null ? c.OrderRow.IdOrder : (int?)null,
                    OrderNumber = c.OrderRow != null && c.OrderRow.Order != null ? (c.OrderRow.Order.Number ?? string.Empty) : string.Empty,
                    IdCompany = c.IdCompany,
                    CompanyName = c.Company != null ? c.Company.RagioneSociale : string.Empty,
                    IdProduct = c.IdProduct,
                    ProductName = c.Product != null ? c.Product.Name : string.Empty,
                    IdArticle = c.IdArticle,
                    ArticleSerial = c.Article != null ? c.Article.SerialNumber : string.Empty,
                    Name = c.Name,
                    State = c.State,
                    Priority = c.Priority,
                    StartDatePlanned = c.StartDatePlanned,
                    EndDatePlanned = c.EndDatePlanned,
                    ExpectedEndDate = c.Phases.Any() ? c.Phases.Max(f => f.EndDate) : c.EndDatePlanned,
                    StartDateActual = c.StartDateActual,
                    EndDateActual = c.EndDateActual,
                    Progress = c.Progress,
                    BudgetHours = c.BudgetHours,
                    IdUserResponsible = c.IdUserResponsible,
                    ResponsibleName = c.UserResponsible != null ? c.UserResponsible.NameComplete : string.Empty,
                    CreatedAt = c.CreatedAt,
                    PhaseCount = c.Phases.Count,
                    TicketCount = c.Phases.SelectMany(f => f.Tickets).Count(),
                    BlockedTicketCount = c.Phases.SelectMany(f => f.Tickets).Count(t => !t.Closed && t.IsBlocked)
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<List<CommessaDTO>> GetByOrderAsync(int orderId)
        {
            var allowed = await _permitsService.GetVisibleCompanyIds();
            var q = _context.Commesse
                .Include(c => c.Company).Include(c => c.Product).Include(c => c.Article)
                .Include(c => c.Phases)
                .Include(c => c.OrderRow).ThenInclude(r => r!.Order)
                .Where(c => c.OrderRow != null && c.OrderRow.IdOrder == orderId);
            if (allowed != null)
                q = q.Where(c => c.IdCompany != null && allowed.Contains(c.IdCompany.Value));
            return await q.Select(c => c.ToDTO()!).ToListAsync();
        }

        public async Task<APIResponseMessage<CommessaDTO>> PostAsync(Commessa item)
        {
            try
            {
                if (!await CanAccessAsync(item.IdCompany))
                    return Fail("Azienda non accessibile", HttpStatusCode.Forbidden);

                // Spostare le date pianificate da qui lascerebbe le fasi dov'erano: l'intestazione
                // direbbe una cosa e il Gantt un'altra, senza che nulla lo segnali. Le date di un
                // piano gia' popolato si cambiano solo da RescheduleAsync, che sposta anche le fasi.
                // Validato prima della transazione: non ha senso aprire un lock per poi rifiutare.
                if (item.Id > 0)
                {
                    var attuale = await _context.Commesse.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == item.Id);

                    if (attuale != null
                        && (item.EndDatePlanned.Date != attuale.EndDatePlanned.Date
                            || item.StartDatePlanned.Date != attuale.StartDatePlanned.Date)
                        && await _context.CommessaFasi.AnyAsync(f => f.IdCommessa == item.Id))
                    {
                        return Fail("Le date di una commessa con fasi si cambiano da Riprogramma, che sposta anche il piano", HttpStatusCode.BadRequest);
                    }
                }

                // La transazione serve alla generazione del codice: e' il lock aperto qui che
                // impedisce a due creazioni concorrenti di leggere lo stesso progressivo.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                int savedId;
                if (item.Id > 0)
                {
                    var existing = await _context.Commesse.FirstOrDefaultAsync(c => c.Id == item.Id);
                    if (existing == null || !await CanAccessAsync(existing.IdCompany))
                        return Fail("Commessa non trovata", HttpStatusCode.NotFound);

                    if (!string.IsNullOrWhiteSpace(item.IdUserResponsible) && !await IsValidResponsibleAsync(item.IdUserResponsible))
                        return Fail("Il responsabile deve essere un utente della HeadCompany", HttpStatusCode.BadRequest);

                    existing.Name = item.Name;
                    existing.Description = item.Description;
                    existing.Note = item.Note;
                    existing.IdUserResponsible = item.IdUserResponsible;
                    existing.Priority = item.Priority;
                    existing.State = item.State;
                    existing.StartDatePlanned = item.StartDatePlanned;
                    existing.EndDatePlanned = item.EndDatePlanned;
                    existing.BudgetHours = item.BudgetHours;
                    savedId = existing.Id;
                }
                else
                {
                    item.CreatedAt = DateTime.Now;
                    item.IdUserCreate = await _permitsService.IdUser();
                    item.IdUserResponsible = await ResolveDefaultResponsibleAsync(item.IdProduct, item.IdUserResponsible, item.IdUserCreate);
                    item.Code = await GenerateCodeAsync(item.CreatedAt);
                    _context.Commesse.Add(item);
                    savedId = 0;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (savedId == 0) savedId = item.Id;
                return Ok(await GetItemAsync(savedId), "Commessa salvata");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(PostAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio della commessa", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state)
        {
            try
            {
                var c = await _context.Commesse.FirstOrDefaultAsync(x => x.Id == id);
                if (c == null || !await CanAccessAsync(c.IdCompany))
                    return Fail("Commessa non trovata", HttpStatusCode.NotFound);

                c.State = state;
                if (state == CommessaStates.InProgress && c.StartDateActual == null)
                    c.StartDateActual = DateTime.Now.Date;
                if (state is CommessaStates.Completed or CommessaStates.Delivered)
                {
                    c.EndDateActual ??= DateTime.Now.Date;
                    if (state == CommessaStates.Completed) c.Progress = 100;
                }
                await _context.SaveChangesAsync();
                await SyncOrderRowStatusAsync(c.IdOrderRow);
                return Ok(await GetItemAsync(id), "Stato aggiornato");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(ChangeStateAsync), EventsTypes.Error, ex);
                return Fail("Errore nel cambio stato", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<List<CommessaDTO>>> StartInternalProductionAsync(InternalProductionRequestDTO req)
        {
            try
            {
                if (req == null || req.IdProduct <= 0)
                    return FailList("Prodotto obbligatorio", HttpStatusCode.BadRequest);

                var product = await _context.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == req.IdProduct);

                if (product == null)
                    return FailList("Prodotto non trovato", HttpStatusCode.NotFound);

                if (product.IdGanttPlan == null)
                    return FailList("Il prodotto non ha un template di produzione", HttpStatusCode.BadRequest);

                // Senza ordine il committente e' l'azienda madre: single-tenant, e' "noi stessi".
                var headCompanyId = await _context.GetHeadCompanyIdAsync();
                if (headCompanyId == null)
                    return FailList("Azienda madre non configurata: impostare una società di tipo HeadCompany", HttpStatusCode.BadRequest);

                if (!await CanAccessAsync(headCompanyId))
                    return FailList("Azienda non accessibile", HttpStatusCode.Forbidden);

                var template = await _context.GanttPhases
                    .Include(p => p.Dependencies)
                    .Include(p => p.TicketTemplates)
                    .Where(p => p.IdGanttPlan == product.IdGanttPlan.Value)
                    .OrderBy(p => p.SortOrder)
                    .AsNoTracking()
                    .ToListAsync();

                if (template.Count == 0)
                    return FailList("Il template di produzione non ha fasi", HttpStatusCode.BadRequest);

                // Nessuna consegna da ordine: la data obiettivo la fornisce l'utente.
                var target = (req.TargetDate ?? DateTime.Today.AddDays(30)).Date;
                int units = Math.Clamp(req.Quantity, 1, 100);
                var now = DateTime.Now;
                var currentUser = await _permitsService.IdUser();
                var responsibleUserId = await ResolveDefaultResponsibleAsync(product.Id, req.IdUserResponsible, currentUser);
                var created = new List<int>();

                // Commessa, fasi, dipendenze e ticket iniziali sono un'unica unita': senza
                // transazione un errore a meta' lascia commesse senza dipendenze, gia' visibili.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                for (int u = 0; u < units; u++)
                {
                    var commessa = new Commessa
                    {
                        Code = await GenerateCodeAsync(now),
                        IdOrderRow = null, // produzione interna: nessuna riga d'ordine
                        IdCompany = headCompanyId,
                        IdProduct = product.Id,
                        IdGanttPlan = product.IdGanttPlan,
                        Name = $"{product.Name} - Interna" + (units > 1 ? $" [{u + 1}/{units}]" : string.Empty),
                        Note = req.Note,
                        State = CommessaStates.Planned,
                        IdUserResponsible = responsibleUserId,
                        IdUserCreate = currentUser,
                        CreatedAt = now,
                        Progress = 0
                    };

                    var (startPlan, phases) = BuildPhasesBackward(template, target);
                    commessa.StartDatePlanned = startPlan;
                    commessa.EndDatePlanned = target;
                    commessa.Phases = phases.Select(p => p.Fase).ToList();

                    _context.Commesse.Add(commessa);
                    await _context.SaveChangesAsync(); // per avere gli Id delle fasi

                    await CloneStructureAsync(phases);
                    await CreateCommessaStartTicketsAsync(commessa, currentUser);
                    created.Add(commessa.Id);
                }

                await transaction.CommitAsync();

                // Nessuna cascata sulla riga d'ordine: qui non esiste.
                var dtos = new List<CommessaDTO>();
                foreach (var id in created)
                {
                    var dto = await GetItemAsync(id);
                    if (dto != null) dtos.Add(dto);
                }

                return new APIResponseMessage<List<CommessaDTO>>
                {
                    State = true,
                    Data = dtos,
                    Message = $"{units} commessa/e interna/e create",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(StartInternalProductionAsync), EventsTypes.Error, ex);
                return FailList("Errore nell'avvio della produzione interna", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId)
        {
            try
            {
                var row = await _context.OrderRows
                    .Include(r => r.Order)
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.Id == orderRowId);

                if (row == null || row.Order == null)
                    return FailList("Riga d'ordine non trovata", HttpStatusCode.NotFound);

                if (!await CanAccessAsync(row.Order.IdCompany))
                    return FailList("Ordine non accessibile", HttpStatusCode.Forbidden);

                if (row.Product?.IdGanttPlan == null)
                    return FailList("Il prodotto non ha un template di produzione: usare 'Conferma pronto'", HttpStatusCode.BadRequest);

                // Template + fasi
                var template = await _context.GanttPhases
                    .Include(p => p.Dependencies)
                    .Include(p => p.TicketTemplates)
                    .Where(p => p.IdGanttPlan == row.Product.IdGanttPlan.Value)
                    .OrderBy(p => p.SortOrder)
                    .AsNoTracking()
                    .ToListAsync();

                // Stessa guardia della produzione interna: un template vuoto genererebbe commesse
                // senza fasi, ferme a 0% per sempre.
                if (template.Count == 0)
                    return FailList("Il template di produzione non ha fasi", HttpStatusCode.BadRequest);

                var delivery = (row.Order.DeliveryDate ?? DateTime.Today.AddDays(30)).Date;
                int units = (int)Math.Max(1, Math.Ceiling(row.Quantity));
                var now = DateTime.Now;
                var currentUser = await _permitsService.IdUser();
                var responsibleUserId = await ResolveDefaultResponsibleAsync(row.IdProduct, null, currentUser);
                var created = new List<int>();

                // Tutte le unita' + la riga d'ordine in un'unica transazione: o la produzione parte
                // per intero, o non parte.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                for (int u = 0; u < units; u++)
                {
                    var commessa = new Commessa
                    {
                        Code = await GenerateCodeAsync(now),
                        IdOrderRow = row.Id,
                        IdCompany = row.Order.IdCompany,
                        IdProduct = row.IdProduct,
                        IdGanttPlan = row.Product.IdGanttPlan,
                        Name = $"{row.Product.Name} - {(string.IsNullOrWhiteSpace(row.Order.Number) ? row.Order.Id.ToString() : row.Order.Number)}" + (units > 1 ? $" [{u + 1}/{units}]" : string.Empty),
                        State = CommessaStates.Planned,
                        IdUserResponsible = responsibleUserId,
                        IdUserCreate = currentUser,
                        CreatedAt = now,
                        Progress = 0
                    };

                    var (startPlan, phases) = BuildPhasesBackward(template, delivery);
                    commessa.StartDatePlanned = startPlan;
                    commessa.EndDatePlanned = delivery;
                    commessa.Phases = phases.Select(p => p.Fase).ToList();

                    _context.Commesse.Add(commessa);
                    await _context.SaveChangesAsync(); // per avere gli Id delle fasi

                    // Ricostruisce dipendenze e gerarchia tra le fasi appena create.
                    await CloneStructureAsync(phases);
                    await CreateCommessaStartTicketsAsync(commessa, currentUser);
                    created.Add(commessa.Id);
                }

                row.ProductionStatus = RowProductionStatus.InProduction;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var dtos = new List<CommessaDTO>();
                foreach (var id in created)
                {
                    var dto = await GetItemAsync(id);
                    if (dto != null) dtos.Add(dto);
                }

                return new APIResponseMessage<List<CommessaDTO>> { State = true, Data = dtos, Message = $"{units} commessa/e create", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(StartProductionAsync), EventsTypes.Error, ex);
                return FailList("Errore nell'avvio produzione", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId)
        {
            try
            {
                var row = await _context.OrderRows.Include(r => r.Order).FirstOrDefaultAsync(r => r.Id == orderRowId);
                if (row == null || row.Order == null)
                    return Fail("Riga d'ordine non trovata", HttpStatusCode.NotFound);
                if (!await CanAccessAsync(row.Order.IdCompany))
                    return Fail("Ordine non accessibile", HttpStatusCode.Forbidden);

                row.ProductionStatus = RowProductionStatus.Ready;
                await _context.SaveChangesAsync();
                return Ok(null, "Riga confermata pronta");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(ConfirmRowReadyAsync), EventsTypes.Error, ex);
                return Fail("Errore nella conferma", HttpStatusCode.InternalServerError);
            }
        }

        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<APIResponseMessage<bool>> DeleteAsync(int id)
        {
            var c = await _context.Commesse.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null)
                return FailDelete("Commessa non trovata", HttpStatusCode.NotFound);
            if (!await CanAccessAsync(c.IdCompany))
                return FailDelete("Commessa non accessibile", HttpStatusCode.Forbidden);
            try
            {
                // Lo smontaggio e' in quattro passaggi vincolati fra loro: a meta' strada
                // resterebbero ticket scollegati da una commessa ancora esistente.
                await using var transaction = await _context.Database.BeginTransactionAsync();

                var fasi = await _context.CommessaFasi.Where(f => f.IdCommessa == id).ToListAsync();
                var faseIds = fasi.Select(f => f.Id).ToList();

                // I ticket sopravvivono alla commessa: perdono solo il legame con la fase.
                var tickets = await _context.Tickets
                    .Where(t => t.IdCommessaFase != null && faseIds.Contains(t.IdCommessaFase.Value))
                    .ToListAsync();
                foreach (var t in tickets) t.IdCommessaFase = null;

                // Commessa -> Fasi e' Cascade, ma le FK che puntano alle fasi sono Restrict:
                // vanno rimosse esplicitamente, altrimenti il cascade viola i vincoli.
                var deps = await _context.CommessaFaseDependencies
                    .Where(d => faseIds.Contains(d.IdFase) || faseIds.Contains(d.IdPredecessorFase))
                    .ToListAsync();
                _context.CommessaFaseDependencies.RemoveRange(deps);

                // Anche l'auto-riferimento della gerarchia WBS e' Restrict: azzerato prima.
                foreach (var f in fasi) f.ParentId = null;
                await _context.SaveChangesAsync();

                _context.CommessaFasi.RemoveRange(fasi);
                await _context.SaveChangesAsync();

                _context.Commesse.Remove(c);
                await _context.SaveChangesAsync();

                await SyncOrderRowStatusAsync(c.IdOrderRow);
                await transaction.CommitAsync();
                return new APIResponseMessage<bool>
                {
                    State = true,
                    Data = true,
                    Message = "Commessa eliminata",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return FailDelete("Errore nell'eliminazione della commessa", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<CommessaDTO>> RescheduleAsync(int id, DateTime newDelivery)
        {
            try
            {
                var commessa = await _context.Commesse
                    .Include(c => c.Phases)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (commessa == null || !await CanAccessAsync(commessa.IdCompany))
                    return Fail("Commessa non trovata", HttpStatusCode.NotFound);

                if (commessa.State is CommessaStates.Delivered or CommessaStates.Cancelled)
                    return Fail("Una commessa consegnata o annullata non si riprogramma", HttpStatusCode.BadRequest);

                // La consegna si arrotonda all'indietro: se cade di sabato, la produzione
                // deve comunque essere chiusa entro il venerdi'.
                var target = newDelivery.Date.PreviousWorkday();
                var delta = commessa.EndDatePlanned.Date.PreviousWorkday().WorkdayDelta(target);
                if (delta == 0)
                    return Fail("La nuova consegna coincide con quella attuale", HttpStatusCode.BadRequest);

                var phases = commessa.Phases.ToList();
                var started = phases.Where(f => f.State != CommessaFaseStates.Pending).ToList();

                // Slittamento in avanti: le fasi gia' avviate hanno date storiche e restano dove
                // sono. All'indietro invece devono seguire, o le fasi ancora da fare verrebbero
                // trascinate sopra di esse.
                var frozen = delta > 0 ? started : new List<CommessaFase>();
                var moving = phases.Where(f => !frozen.Contains(f)).ToList();

                foreach (var f in moving)
                {
                    f.StartDate = f.StartDate.ShiftWorkdays(delta);
                    f.EndDate = f.IsMilestone ? f.StartDate : f.EndDate.ShiftWorkdays(delta);
                }

                commessa.EndDatePlanned = target;
                commessa.StartDatePlanned = phases.Count > 0
                    ? phases.Min(f => f.StartDate).Date
                    : commessa.StartDatePlanned.ShiftWorkdays(delta);

                await _context.SaveChangesAsync();

                var verso = delta > 0 ? "in avanti" : "indietro";
                var giorni = Math.Abs(delta) == 1 ? "1 giorno lavorativo" : $"{Math.Abs(delta)} giorni lavorativi";
                var note = new List<string>();

                if (frozen.Count > 0)
                    note.Add($"{frozen.Count} gia' avviate restano alle date attuali");
                if (delta < 0 && started.Count > 0)
                    note.Add($"{started.Count} gia' avviate sono state spostate per non sovrapporsi");

                var nelPassato = moving.Count(f => f.StartDate.Date < DateTime.Today);
                if (nelPassato > 0)
                    note.Add($"attenzione: {nelPassato} fasi ora iniziano prima di oggi");

                var message = $"Piano spostato di {giorni} {verso}: {moving.Count} fasi riprogrammate"
                    + (note.Count > 0 ? $" ({string.Join("; ", note)})" : string.Empty);

                return Ok(await GetItemAsync(id), message);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(RescheduleAsync), EventsTypes.Error, ex);
                return Fail("Errore nella riprogrammazione della commessa", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<CommessaDTO>> RebuildPlanFromTemplateAsync(int id, DateTime? newDelivery)
        {
            try
            {
                var commessa = await _context.Commesse
                    .Include(c => c.Phases)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (commessa == null || !await CanAccessAsync(commessa.IdCompany))
                    return Fail("Commessa non trovata", HttpStatusCode.NotFound);

                if (commessa.State is CommessaStates.Delivered or CommessaStates.Cancelled)
                    return Fail("Una commessa consegnata o annullata non si ripianifica", HttpStatusCode.BadRequest);

                // Il ripristino cancella le fasi: su lavoro gia' avviato distruggerebbe lo storico,
                // e per quel caso esiste la riprogrammazione, che le date le sposta e basta.
                var avviate = commessa.Phases.Count(f => f.State != CommessaFaseStates.Pending);
                if (avviate > 0)
                    return Fail($"Ci sono {avviate} fasi gia' avviate o concluse: usa Riprogramma, il ripristino e' consentito solo su un piano intatto", HttpStatusCode.BadRequest);

                if (commessa.IdGanttPlan == null)
                    return Fail("La commessa non ha un template di produzione da cui ripartire", HttpStatusCode.BadRequest);

                var template = await _context.GanttPhases
                    .Include(p => p.Dependencies)
                    .Include(p => p.TicketTemplates)
                    .Where(p => p.IdGanttPlan == commessa.IdGanttPlan.Value)
                    .OrderBy(p => p.SortOrder)
                    .AsNoTracking()
                    .ToListAsync();

                if (template.Count == 0)
                    return Fail("Il template di produzione non ha fasi", HttpStatusCode.BadRequest);

                var target = (newDelivery ?? commessa.EndDatePlanned).Date.PreviousWorkday();
                var vecchie = commessa.Phases.ToList();
                var faseIds = vecchie.Select(f => f.Id).ToList();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Stessa politica della cancellazione di una fase: i ticket sopravvivono, perdono
                // solo il legame. Cancellarli butterebbe via lavoro tracciato fuori dal piano.
                var tickets = await _context.Tickets
                    .Where(t => t.IdCommessaFase != null && faseIds.Contains(t.IdCommessaFase.Value))
                    .ToListAsync();
                foreach (var t in tickets) t.IdCommessaFase = null;

                // Le FK verso le fasi sono Restrict: dipendenze e gerarchia vanno sciolte prima.
                var deps = await _context.CommessaFaseDependencies
                    .Where(d => faseIds.Contains(d.IdFase) || faseIds.Contains(d.IdPredecessorFase))
                    .ToListAsync();
                _context.CommessaFaseDependencies.RemoveRange(deps);
                foreach (var f in vecchie) f.ParentId = null;
                await _context.SaveChangesAsync();

                _context.CommessaFasi.RemoveRange(vecchie);
                await _context.SaveChangesAsync();

                var (startPlan, phases) = BuildPhasesBackward(template, target);
                commessa.StartDatePlanned = startPlan;
                commessa.EndDatePlanned = target;
                commessa.Phases = phases.Select(p => p.Fase).ToList();
                await _context.SaveChangesAsync(); // per avere gli Id delle fasi

                await CloneStructureAsync(phases);
                await transaction.CommitAsync();

                var note = tickets.Count > 0
                    ? $" ({tickets.Count} ticket restano senza fase collegata)"
                    : string.Empty;

                return Ok(await GetItemAsync(id), $"Piano ricostruito dal modello: {phases.Count} fasi{note}");
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(RebuildPlanFromTemplateAsync), EventsTypes.Error, ex);
                return Fail("Errore nella ricostruzione del piano", HttpStatusCode.InternalServerError);
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────────

        private static APIResponseMessage<CommessaDTO> Fail(string m, HttpStatusCode c) => new() { State = false, Message = m, Code = c };
        private static APIResponseMessage<CommessaDTO> Ok(CommessaDTO? d, string m) => new() { State = true, Data = d, Message = m, Code = HttpStatusCode.OK };
        private static APIResponseMessage<List<CommessaDTO>> FailList(string m, HttpStatusCode c) => new() { State = false, Message = m, Code = c };
        private static APIResponseMessage<bool> FailDelete(string m, HttpStatusCode c) => new() { State = false, Message = m, Code = c };

        /// <summary>
        /// Costruisce le fasi con date assolute, schedulazione all'indietro dalla consegna.
        /// Restituisce l'accoppiamento fase-modello → fase creata: e' quello che permette di
        /// ricostruire dipendenze e gerarchia senza passare dal SortOrder, che non e' univoco.
        /// </summary>
        internal static (DateTime start, List<(GanttPhase Template, CommessaFase Fase)> phases) BuildPhasesBackward(List<GanttPhase> template, DateTime delivery)
        {
            if (template.Count == 0)
                return (delivery, new List<(GanttPhase, CommessaFase)>());

            var byId = template.ToDictionary(t => t.Id);
            var preds = template.ToDictionary(t => t.Id, t => t.Dependencies.Select(d => (d.IdPredecessorPhase, d.LagDays)).ToList());
            var succIds = template.ToDictionary(t => t.Id, t => new List<int>());
            foreach (var t in template)
                foreach (var d in t.Dependencies)
                    if (succIds.ContainsKey(d.IdPredecessorPhase))
                        succIds[d.IdPredecessorPhase].Add(t.Id);

            // forward pass: earliest-start offset (giorni LAVORATIVI, come DurationDays del template)
            var indeg = template.ToDictionary(t => t.Id, t => preds[t.Id].Count);
            var queue = new Queue<int>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var es = template.ToDictionary(t => t.Id, t => 0);
            var order = new List<int>();
            while (queue.Count > 0)
            {
                var id = queue.Dequeue(); order.Add(id);
                int ef = es[id] + Math.Max(byId[id].IsMilestone ? 0 : 1, byId[id].DurationDays);
                foreach (var s in succIds[id])
                {
                    var lag = preds[s].FirstOrDefault(p => p.IdPredecessorPhase == id).LagDays;
                    es[s] = Math.Max(es[s], ef + lag);
                    if (--indeg[s] == 0) queue.Enqueue(s);
                }
            }
            // fallback in caso di ciclo: ordine per SortOrder cumulativo
            if (order.Count != template.Count)
            {
                int acc = 0;
                foreach (var t in template) { es[t.Id] = acc; acc += Math.Max(1, t.DurationDays); }
            }

            // Gli offset sono in giorni lavorativi: vanno convertiti in date con lo stesso calendario,
            // altrimenti una fase di 5 giorni a cavallo del weekend scade di sabato e l'errore si
            // accumula fase dopo fase fino a settimane sull'intera commessa.
            int projectDuration = template.Max(t => es[t.Id] + Math.Max(t.IsMilestone ? 0 : 1, t.DurationDays));
            var start = delivery.SubtractWorkdays(projectDuration);
            if (start < DateTime.Today) start = DateTime.Today; // fallback in avanti se in ritardo
            start = start.NextWorkday();

            var phases = new List<(GanttPhase Template, CommessaFase Fase)>();
            foreach (var t in template.OrderBy(t => t.SortOrder))
            {
                var s = start.AddWorkdays(es[t.Id]);
                var ticketTemplates = t.TicketTemplates?
                    .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                    .ToList() ?? new List<GanttPhaseTicketTemplate>();
                var hasTicketWork = t.IdTicketType != null || ticketTemplates.Count > 0;

                var fase = new CommessaFase
                {
                    Name = t.Name,
                    Description = t.Description,
                    StartDate = s,
                    EndDate = t.IsMilestone ? s : s.AddWorkdays(Math.Max(1, t.DurationDays) - 1),
                    SortOrder = t.SortOrder,
                    IsMilestone = t.IsMilestone,
                    Color = t.Color,
                    IdTicketType = t.IdTicketType,
                    IdGroup = t.IdGroup,
                    CompletionMode = hasTicketWork ? CommessaFaseCompletionMode.AllTicketsClosed : CommessaFaseCompletionMode.Manual,
                    AutoCreateTicketOnTake = hasTicketWork,
                    RequiresTicket = hasTicketWork,
                    State = CommessaFaseStates.Pending,
                    Progress = 0
                };

                if (ticketTemplates.Count > 0)
                {
                    foreach (var tt in ticketTemplates)
                        fase.TicketPlans.Add(new CommessaFaseTicketPlan
                        {
                            IdGanttPhaseTicketTemplate = tt.Id,
                            Title = tt.Title,
                            Description = tt.Description,
                            IdTicketType = tt.IdTicketType,
                            IdGroupAssigned = tt.IdGroupAssigned,
                            Required = tt.Required,
                            AutoCreateMode = tt.AutoCreateMode,
                            SortOrder = tt.SortOrder
                        });
                }
                else if (t.IdTicketType != null)
                {
                    fase.TicketPlans.Add(new CommessaFaseTicketPlan
                    {
                        Title = t.Name,
                        Description = t.Description,
                        IdTicketType = t.IdTicketType.Value,
                        IdGroupAssigned = t.IdGroup,
                        Required = true,
                        AutoCreateMode = ProductionTicketAutoCreateMode.OnPhaseStart,
                        SortOrder = 1
                    });
                }

                phases.Add((t, fase));
            }
            return (start, phases);
        }

        /// <summary>
        /// Ricrea dipendenze e gerarchia WBS tra le fasi della commessa. Va eseguito dopo il primo
        /// SaveChanges, quando EF ha valorizzato gli Id delle fasi appena inserite.
        /// La mappa e' indicizzata sull'Id della fase-modello (chiave primaria, univoca per
        /// definizione): il SortOrder e' modificabile a mano e due fasi possono condividerlo.
        /// </summary>
        private async Task CloneStructureAsync(List<(GanttPhase Template, CommessaFase Fase)> built)
        {
            var byTemplateId = built.ToDictionary(p => p.Template.Id, p => p.Fase);

            foreach (var (t, fase) in built)
            {
                // Dipendenze (vincolo temporale)
                foreach (var d in t.Dependencies)
                {
                    if (!byTemplateId.TryGetValue(d.IdPredecessorPhase, out var pred)) continue;
                    _context.CommessaFaseDependencies.Add(new CommessaFaseDependency
                    {
                        IdFase = fase.Id,
                        IdPredecessorFase = pred.Id,
                        LagDays = d.LagDays,
                        Type = d.Type
                    });
                }

                // Gerarchia WBS (raggruppamento, nessun effetto sulle date)
                if (t.ParentId != null
                    && byTemplateId.TryGetValue(t.ParentId.Value, out var parent)
                    && parent.Id != fase.Id)
                {
                    fase.ParentId = parent.Id;
                }
            }
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Crea subito i ticket pianificati con regola "a inizio commessa", lasciando pero'
        /// le fasi in Pending: aprire il ticket non equivale a dichiarare iniziata la fase.
        /// </summary>
        private async Task CreateCommessaStartTicketsAsync(Commessa commessa, string currentUser)
        {
            var plans = await _context.CommessaFaseTicketPlans
                .Include(p => p.CommessaFase)
                .Where(p => p.CommessaFase != null
                    && p.CommessaFase.IdCommessa == commessa.Id
                    && p.AutoCreateMode == ProductionTicketAutoCreateMode.OnCommessaStart
                    && p.IdTicket == null)
                .OrderBy(p => p.CommessaFase!.SortOrder)
                .ThenBy(p => p.SortOrder)
                .ToListAsync();

            foreach (var plan in plans)
            {
                var fase = plan.CommessaFase!;
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
                    IdUserOpened = currentUser,
                    DateOpened = DateTime.Now,
                    Date = DateTime.Today,
                    DateEnd = fase.EndDate,
                    DateExpired = fase.EndDate,
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
            }

            if (plans.Count > 0)
                await _context.SaveChangesAsync();
        }

        /// <summary>Allinea lo stato produzione della riga d'ordine allo stato delle sue commesse.</summary>
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

        private async Task<string?> ResolveDefaultResponsibleAsync(int? idProduct, string? requestedUserId, string? currentUserId)
        {
            if (await IsValidResponsibleAsync(requestedUserId))
                return requestedUserId;

            var productResponsibleId = idProduct == null
                ? null
                : await _context.Products
                    .AsNoTracking()
                    .Where(product => product.Id == idProduct)
                    .Select(product => product.IdDefaultCommessaResponsible)
                    .FirstOrDefaultAsync();

            if (await IsValidResponsibleAsync(productResponsibleId))
                return productResponsibleId;

            if (await IsValidResponsibleAsync(currentUserId))
                return currentUserId;

            return null;
        }

        private async Task<bool> IsValidResponsibleAsync(string? idUser)
        {
            if (string.IsNullOrWhiteSpace(idUser))
                return false;

            var headCompanyId = await _context.GetHeadCompanyIdAsync();
            if (headCompanyId == null)
                return false;

            return await _context.Users
                .AsNoTracking()
                .AnyAsync(user => user.Id == idUser && !user.IsDeleted && user.IdCompany == headCompanyId);
        }

        /// <summary>
        /// Numerazione progressiva per anno. UPDLOCK+HOLDLOCK tiene il lock sull'intervallo dei
        /// codici dell'anno fino alla fine della transazione: due generazioni concorrenti si
        /// serializzano invece di leggere lo stesso massimo e produrre lo stesso codice.
        /// Vale solo con una transazione aperta (altrimenti il lock cade subito), quindi tutti i
        /// chiamanti ne aprono una. L'indice univoco IX_Commesse_Code resta la rete di sicurezza.
        /// </summary>
        private async Task<string> GenerateCodeAsync(DateTime date)
        {
            string prefix = $"CM-{date.Year}-";
            var codes = await _context.Database
                .SqlQueryRaw<string>(
                    "SELECT Code AS Value FROM Commesse WITH (UPDLOCK, HOLDLOCK) WHERE Code LIKE {0}",
                    prefix + "%")
                .ToListAsync();

            int max = 0;
            foreach (var n in codes)
                if (n.Length > prefix.Length && int.TryParse(n.Substring(prefix.Length), out var v) && v > max)
                    max = v;
            return prefix + (max + 1).ToString("D4");
        }

        private async Task<bool> CanAccessAsync(int? idCompany)
        {
            var allowed = await _permitsService.GetVisibleCompanyIds();
            if (allowed == null) return true;
            return idCompany != null && allowed.Contains(idCompany.Value);
        }

        private async Task<IQueryable<Commessa>?> FilterItems(CommessaFilter? args)
        {
            try
            {
                var items = _context.Commesse
                    .Include(c => c.Company).Include(c => c.Product).Include(c => c.Article)
                    .Include(c => c.UserResponsible)
                    .Include(c => c.OrderRow).ThenInclude(r => r!.Order)
                    .AsQueryable();

                var allowed = await _permitsService.GetVisibleCompanyIds();
                if (allowed != null)
                    items = items.Where(c => c.IdCompany != null && allowed.Contains(c.IdCompany.Value));

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                    items = items.OrderBy(args.OrderBy);
                else
                    items = items.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);

                if (args?.IdCompany != null) items = items.Where(c => c.IdCompany == args.IdCompany);
                if (args?.IdOrderRow != null) items = items.Where(c => c.IdOrderRow == args.IdOrderRow);
                if (args?.IdOrder != null) items = items.Where(c => c.OrderRow != null && c.OrderRow.IdOrder == args.IdOrder);
                if (args?.IdUserResponsible != null) items = items.Where(c => c.IdUserResponsible == args.IdUserResponsible);
                if (args?.State != null) items = items.Where(c => c.State == args.State);
                if (args?.ExpectedLate == true)
                {
                    items = items.Where(c =>
                        c.State != CommessaStates.Completed
                        && c.State != CommessaStates.Delivered
                        && c.State != CommessaStates.Cancelled
                        && c.Phases.Any(f => f.EndDate > c.EndDatePlanned));
                }
                if (!string.IsNullOrWhiteSpace(args?.Search))
                {
                    var s = args.Search.Trim();
                    items = items.Where(c => (c.Code != null && c.Code.Contains(s)) || (c.Name != null && c.Name.Contains(s))
                        || (c.Company != null && c.Company.RagioneSociale.Contains(s)));
                }
                if (!string.IsNullOrWhiteSpace(args?.Filter))
                    items = items.Where(args.Filter);

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(FilterItems), EventsTypes.Error, ex);
                return null;
            }
        }
    }
}
