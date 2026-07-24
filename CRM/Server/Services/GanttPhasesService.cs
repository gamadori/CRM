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
    /// Fasi-modello dei template <see cref="GanttPlan"/>. Durate relative, dipendenze, tipo-ticket
    /// e gruppo abilitato. Non sono legate ad aziende: sono template globali.
    /// </summary>
    public class GanttPhasesService : IGanttPhasesService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;

        public GanttPhasesService(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        public async Task<List<GanttPhaseDTO>?> GetTreeAsync(int idGanttPlan)
        {
            try
            {
                var phases = await _context.GanttPhases
                    .Include(p => p.Dependencies)
                    .Where(p => p.IdGanttPlan == idGanttPlan)
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
                    .AsNoTracking()
                    .ToListAsync();

                return phases.Select(p => p.ToDTO()).ToList();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(GetTreeAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<GanttPhaseDTO>> SaveAsync(GanttPhaseDTO dto)
        {
            try
            {
                dto.Name = (dto.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Fail("Nome fase obbligatorio", HttpStatusCode.BadRequest);

                GanttPhase entity;
                if (dto.Id > 0)
                {
                    entity = await _context.GanttPhases.FirstOrDefaultAsync(p => p.Id == dto.Id);
                    if (entity == null)
                        return Fail("Fase non trovata", HttpStatusCode.NotFound);

                    entity.Name = dto.Name;
                    entity.Description = dto.Description;
                    entity.ParentId = dto.ParentId;
                    entity.DurationDays = Math.Max(0, dto.DurationDays);
                    entity.SortOrder = dto.SortOrder;
                    entity.IsMilestone = dto.IsMilestone;
                    entity.IdTicketType = dto.IdTicketType;
                    entity.IdGroup = dto.IdGroup;
                    entity.Color = dto.Color;
                }
                else
                {
                    entity = dto.ToEntity();
                    if (entity.SortOrder == 0)
                        entity.SortOrder = (await _context.GanttPhases.Where(p => p.IdGanttPlan == dto.IdGanttPlan)
                            .Select(p => (int?)p.SortOrder).MaxAsync() ?? 0) + 1;
                    _context.GanttPhases.Add(entity);
                }

                await _context.SaveChangesAsync();
                return new APIResponseMessage<GanttPhaseDTO> { State = true, Data = entity.ToDTO(), Message = "Fase salvata", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(SaveAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio della fase", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<bool> BulkSaveAsync(List<GanttPhaseDTO> dtos)
        {
            try
            {
                if (dtos == null || dtos.Count == 0) return true;
                var ids = dtos.Select(d => d.Id).ToList();
                var entities = await _context.GanttPhases.Where(p => ids.Contains(p.Id)).ToListAsync();
                var map = entities.ToDictionary(e => e.Id);
                foreach (var dto in dtos)
                {
                    if (!map.TryGetValue(dto.Id, out var e)) continue;
                    e.DurationDays = Math.Max(0, dto.DurationDays);
                    e.SortOrder = dto.SortOrder;
                    e.ParentId = dto.ParentId;
                    e.IsMilestone = dto.IsMilestone;
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(BulkSaveAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int phaseId)
        {
            try
            {
                var phase = await _context.GanttPhases.FirstOrDefaultAsync(p => p.Id == phaseId);
                if (phase == null) return false;

                var deps = await _context.GanttPhaseDependencies
                    .Where(d => d.IdPhase == phaseId || d.IdPredecessorPhase == phaseId)
                    .ToListAsync();
                _context.GanttPhaseDependencies.RemoveRange(deps);

                var children = await _context.GanttPhases.Where(p => p.ParentId == phaseId).ToListAsync();
                foreach (var c in children)
                    c.ParentId = phase.ParentId;

                _context.GanttPhases.Remove(phase);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        public async Task<APIResponseMessage<GanttPhaseDependencyDTO>> AddDependencyAsync(GanttPhaseDependencyDTO dto)
        {
            try
            {
                if (dto.IdPhase == dto.IdPredecessorPhase)
                    return new() { State = false, Message = "Una fase non puo' dipendere da se stessa", Code = HttpStatusCode.BadRequest };

                var already = await _context.GanttPhaseDependencies
                    .AnyAsync(d => d.IdPhase == dto.IdPhase && d.IdPredecessorPhase == dto.IdPredecessorPhase);
                if (already)
                    return new() { State = false, Message = "Dipendenza gia' presente", Code = HttpStatusCode.Conflict };

                var entity = new GanttPhaseDependency
                {
                    IdPhase = dto.IdPhase,
                    IdPredecessorPhase = dto.IdPredecessorPhase,
                    LagDays = dto.LagDays,
                    Type = dto.Type
                };
                _context.GanttPhaseDependencies.Add(entity);
                await _context.SaveChangesAsync();
                return new() { State = true, Data = entity.ToDTO(), Message = "Dipendenza creata", Code = HttpStatusCode.OK };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(AddDependencyAsync), EventsTypes.Error, ex);
                return new() { State = false, Message = "Errore nella creazione della dipendenza", Code = HttpStatusCode.InternalServerError };
            }
        }

        public async Task<bool> RemoveDependencyAsync(int dependencyId)
        {
            try
            {
                var dep = await _context.GanttPhaseDependencies.FirstOrDefaultAsync(d => d.Id == dependencyId);
                if (dep == null) return false;
                _context.GanttPhaseDependencies.Remove(dep);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPhasesService), nameof(RemoveDependencyAsync), EventsTypes.Error, ex);
                return false;
            }
        }

        private static APIResponseMessage<GanttPhaseDTO> Fail(string msg, HttpStatusCode code)
            => new() { State = false, Message = msg, Code = code };
    }
}
