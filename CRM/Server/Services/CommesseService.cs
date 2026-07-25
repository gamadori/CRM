using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
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
                        StartDateActual = c.StartDateActual,
                        EndDateActual = c.EndDateActual,
                        Progress = c.Progress,
                        BudgetHours = c.BudgetHours,
                        IdUserResponsible = c.IdUserResponsible,
                        ResponsibleName = c.UserResponsible != null ? c.UserResponsible.NameComplete : string.Empty,
                        CreatedAt = c.CreatedAt,
                        PhaseCount = c.Phases.Count
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
                return await items.Select(c => c.ToDTO()!).ToListAsync();
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

                int savedId;
                if (item.Id > 0)
                {
                    var existing = await _context.Commesse.FirstOrDefaultAsync(c => c.Id == item.Id);
                    if (existing == null || !await CanAccessAsync(existing.IdCompany))
                        return Fail("Commessa non trovata", HttpStatusCode.NotFound);

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
                    item.Code = await GenerateCodeAsync(item.CreatedAt);
                    _context.Commesse.Add(item);
                    savedId = 0;
                }

                await _context.SaveChangesAsync();
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
                    .Where(p => p.IdGanttPlan == row.Product.IdGanttPlan.Value)
                    .OrderBy(p => p.SortOrder)
                    .AsNoTracking()
                    .ToListAsync();

                var delivery = (row.Order.DeliveryDate ?? DateTime.Today.AddDays(30)).Date;
                int units = (int)Math.Max(1, Math.Ceiling(row.Quantity));
                var now = DateTime.Now;
                var currentUser = await _permitsService.IdUser();
                var created = new List<int>();

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
                        IdUserResponsible = row.Order.IdUser,
                        IdUserCreate = currentUser,
                        CreatedAt = now,
                        Progress = 0
                    };

                    var (startPlan, phases) = BuildPhasesBackward(template, delivery);
                    commessa.StartDatePlanned = startPlan;
                    commessa.EndDatePlanned = delivery;
                    commessa.Phases = phases;

                    _context.Commesse.Add(commessa);
                    await _context.SaveChangesAsync(); // per avere gli Id delle fasi

                    // Ricostruisce dipendenze e gerarchia tra le fasi appena create (map template->nuove per SortOrder)
                    await CloneStructureAsync(template, commessa);
                    created.Add(commessa.Id);
                }

                row.ProductionStatus = RowProductionStatus.InProduction;
                await _context.SaveChangesAsync();

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
        public async Task<bool> DeleteAsync(int id)
        {
            var c = await _context.Commesse.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null || !await CanAccessAsync(c.IdCompany))
                return false;
            try
            {
                var tickets = await _context.Tickets.Where(t => t.CommessaFase!.IdCommessa == id).ToListAsync();
                foreach (var t in tickets) t.IdCommessaFase = null;
                _context.Commesse.Remove(c);
                await _context.SaveChangesAsync();
                await SyncOrderRowStatusAsync(c.IdOrderRow);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommesseService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        // ─── Helper ──────────────────────────────────────────────────────────────

        private static APIResponseMessage<CommessaDTO> Fail(string m, HttpStatusCode c) => new() { State = false, Message = m, Code = c };
        private static APIResponseMessage<CommessaDTO> Ok(CommessaDTO? d, string m) => new() { State = true, Data = d, Message = m, Code = HttpStatusCode.OK };
        private static APIResponseMessage<List<CommessaDTO>> FailList(string m, HttpStatusCode c) => new() { State = false, Message = m, Code = c };

        /// <summary>Costruisce le fasi con date assolute, schedulazione all'indietro dalla consegna.</summary>
        private static (DateTime start, List<CommessaFase> phases) BuildPhasesBackward(List<GanttPhase> template, DateTime delivery)
        {
            if (template.Count == 0)
                return (delivery, new List<CommessaFase>());

            var byId = template.ToDictionary(t => t.Id);
            var preds = template.ToDictionary(t => t.Id, t => t.Dependencies.Select(d => (d.IdPredecessorPhase, d.LagDays)).ToList());
            var succIds = template.ToDictionary(t => t.Id, t => new List<int>());
            foreach (var t in template)
                foreach (var d in t.Dependencies)
                    if (succIds.ContainsKey(d.IdPredecessorPhase))
                        succIds[d.IdPredecessorPhase].Add(t.Id);

            // forward pass: earliest-start offset (giorni)
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

            int projectDuration = template.Max(t => es[t.Id] + Math.Max(t.IsMilestone ? 0 : 1, t.DurationDays));
            var start = delivery.AddDays(-projectDuration);
            if (start < DateTime.Today) start = DateTime.Today; // fallback in avanti se in ritardo

            var phases = new List<CommessaFase>();
            foreach (var t in template.OrderBy(t => t.SortOrder))
            {
                var s = start.AddDays(es[t.Id]);
                phases.Add(new CommessaFase
                {
                    Name = t.Name,
                    Description = t.Description,
                    StartDate = s,
                    EndDate = t.IsMilestone ? s : s.AddDays(Math.Max(1, t.DurationDays) - 1),
                    SortOrder = t.SortOrder,
                    IsMilestone = t.IsMilestone,
                    Color = t.Color,
                    IdTicketType = t.IdTicketType,
                    IdGroup = t.IdGroup,
                    State = CommessaFaseStates.Pending,
                    Progress = 0
                });
            }
            return (start, phases);
        }

        /// <summary>
        /// Ricrea dipendenze e gerarchia WBS tra le fasi della commessa mappando dal template per SortOrder.
        /// Va eseguito dopo il primo SaveChanges, quando le fasi hanno gia' un Id.
        /// </summary>
        private async Task CloneStructureAsync(List<GanttPhase> template, Commessa commessa)
        {
            var newFasi = await _context.CommessaFasi
                .Where(f => f.IdCommessa == commessa.Id)
                .ToListAsync();
            var newBySort = newFasi.ToDictionary(f => f.SortOrder, f => f.Id);
            var tmplById = template.ToDictionary(t => t.Id);

            foreach (var t in template)
            {
                if (!newBySort.TryGetValue(t.SortOrder, out var newFaseId)) continue;

                // Dipendenze (vincolo temporale)
                foreach (var d in t.Dependencies)
                {
                    if (!tmplById.TryGetValue(d.IdPredecessorPhase, out var predTmpl)) continue;
                    if (!newBySort.TryGetValue(predTmpl.SortOrder, out var newPredId)) continue;
                    _context.CommessaFaseDependencies.Add(new CommessaFaseDependency
                    {
                        IdFase = newFaseId,
                        IdPredecessorFase = newPredId,
                        LagDays = d.LagDays,
                        Type = d.Type
                    });
                }

                // Gerarchia WBS (raggruppamento, nessun effetto sulle date)
                if (t.ParentId != null
                    && tmplById.TryGetValue(t.ParentId.Value, out var parentTmpl)
                    && newBySort.TryGetValue(parentTmpl.SortOrder, out var newParentId)
                    && newParentId != newFaseId)
                {
                    var fase = newFasi.First(f => f.Id == newFaseId);
                    fase.ParentId = newParentId;
                }
            }
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

        private async Task<string> GenerateCodeAsync(DateTime date)
        {
            string prefix = $"CM-{date.Year}-";
            var numbers = await _context.Commesse.Where(c => c.Code != null && c.Code.StartsWith(prefix)).Select(c => c.Code!).ToListAsync();
            int max = 0;
            foreach (var n in numbers)
                if (int.TryParse(n.Substring(prefix.Length), out var v) && v > max) max = v;
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
