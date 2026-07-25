using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
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
                    .Include(f => f.UserTakenBy)
                    .Include(f => f.TicketType)
                    .Include(f => f.Group)
                    .Where(f => f.IdCommessa == idCommessa)
                    .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                    .AsNoTracking()
                    .ToListAsync();

                var dtos = fasi.Select(f => f.ToDTO()).ToList();
                ComputeCriticalPath(dtos);
                return dtos;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(GetTreeAsync), EventsTypes.Error, ex);
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
                if (dto.Id > 0)
                {
                    entity = await _context.CommessaFasi.FirstOrDefaultAsync(f => f.Id == dto.Id && f.IdCommessa == dto.IdCommessa);
                    if (entity == null)
                        return Fail("Fase non trovata", HttpStatusCode.NotFound);

                    entity.Name = dto.Name;
                    entity.Description = dto.Description;
                    entity.ParentId = dto.ParentId;
                    entity.StartDate = dto.StartDate;
                    entity.EndDate = dto.EndDate;
                    entity.Progress = Math.Clamp(dto.Progress, 0, 100);
                    entity.SortOrder = dto.SortOrder;
                    entity.IsMilestone = dto.IsMilestone;
                    entity.Color = dto.Color;
                    entity.IdTicketType = dto.IdTicketType;
                    entity.IdGroup = dto.IdGroup;
                    entity.State = dto.State;
                }
                else
                {
                    entity = dto.ToEntity();
                    entity.Progress = Math.Clamp(dto.Progress, 0, 100);
                    if (entity.SortOrder == 0)
                        entity.SortOrder = await NextSortOrderAsync(dto.IdCommessa);
                    _context.CommessaFasi.Add(entity);
                }

                await _context.SaveChangesAsync();
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
                    .Where(f => f.IdCommessa == idCommessa && ids.Contains(f.Id))
                    .ToListAsync();

                var map = entities.ToDictionary(e => e.Id);
                foreach (var dto in dtos)
                {
                    if (!map.TryGetValue(dto.Id, out var e))
                        continue;
                    Normalize(dto);
                    e.StartDate = dto.StartDate;
                    e.EndDate = dto.EndDate;
                    e.Progress = Math.Clamp(dto.Progress, 0, 100);
                    e.SortOrder = dto.SortOrder;
                    e.ParentId = dto.ParentId;
                    e.IsMilestone = dto.IsMilestone;
                }

                await _context.SaveChangesAsync();
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
                var fase = await _context.CommessaFasi
                    .Include(f => f.Tickets)
                    .FirstOrDefaultAsync(f => f.Id == faseId.Value);
                if (fase == null) return;

                int total = fase.Tickets?.Count ?? 0;
                if (total > 0)
                {
                    int closed = fase.Tickets!.Count(t => t.Closed);
                    fase.Progress = (int)Math.Round(closed * 100.0 / total);
                    // Con almeno un ticket la fase è avviata; completata solo quando tutti sono chiusi.
                    fase.State = closed == total ? CommessaFaseStates.Done : CommessaFaseStates.InProgress;
                    if (fase.State == CommessaFaseStates.InProgress && fase.TakenAt == null)
                        fase.TakenAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
                await RecomputeCommessaProgressAsync(fase.IdCommessa);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CommessaFasiService), nameof(RecomputeFaseProgressAsync), EventsTypes.Error, ex);
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
        /// Avanzamento commessa = media pesata sui giorni di tutte le fasi (milestone escluse).
        /// Anche una fase che ha sotto-fasi conta: qui la gerarchia e' solo raggruppamento e ogni
        /// fase e' lavoro reale 1:1 con un ticket, quindi deve pesare sulla percentuale.
        /// </summary>
        private async Task RecomputeCommessaProgressAsync(int idCommessa)
        {
            var fasi = await _context.CommessaFasi
                .Where(f => f.IdCommessa == idCommessa)
                .Select(f => new { f.StartDate, f.EndDate, f.Progress, f.IsMilestone })
                .ToListAsync();

            var counted = fasi.Where(f => !f.IsMilestone).ToList();

            int progress = 0;
            if (counted.Count > 0)
            {
                double weightSum = 0, acc = 0;
                foreach (var f in counted)
                {
                    double w = Math.Max(1, (f.EndDate.Date - f.StartDate.Date).TotalDays + 1);
                    weightSum += w;
                    acc += w * Math.Clamp(f.Progress, 0, 100);
                }
                if (weightSum > 0)
                    progress = (int)Math.Round(acc / weightSum);
            }

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
        private static void ComputeCriticalPath(List<CommessaFaseDTO> fasi)
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

            double Dur(CommessaFaseDTO f) => f.IsMilestone ? 0 : Math.Max(1, (f.EndDate.Date - f.StartDate.Date).TotalDays + 1);

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
