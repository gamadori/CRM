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
    public class GanttPlansService : IGanttPlansService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public GanttPlansService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<List<GanttPlanDTO>?> GetListAsync(GanttPlanFilter? args = null)
        {
            try
            {
                var items = _context.GanttPlans.AsNoTracking().AsQueryable();

                if (args?.State != null)
                    items = items.Where(x => x.State == args.State);
                else
                    items = items.Where(x => x.State != GanttPlanStates.Archived);

                if (!string.IsNullOrWhiteSpace(args?.Search))
                {
                    var search = args.Search.Trim();
                    items = items.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
                }

                return await items
                    .OrderBy(x => x.Name)
                    .Select(x => x.ToDTO()!)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPlansService), nameof(GetListAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<GanttPlanDTO?> GetItemAsync(int id)
        {
            try
            {
                var plan = await _context.GanttPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                return plan.ToDTO();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPlansService), nameof(GetItemAsync), EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<APIResponseMessage<GanttPlanDTO>> SaveAsync(GanttPlanDTO dto)
        {
            try
            {
                dto.Name = (dto.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return Fail("Nome del piano Gantt obbligatorio", HttpStatusCode.BadRequest);

                GanttPlan plan;
                if (dto.Id > 0)
                {
                    plan = await _context.GanttPlans.FirstOrDefaultAsync(x => x.Id == dto.Id);
                    if (plan == null)
                        return Fail("Piano Gantt non trovato", HttpStatusCode.NotFound);

                    plan.Name = dto.Name;
                    plan.Description = dto.Description;
                    plan.State = dto.State;
                    plan.StartDate = dto.StartDate;
                    plan.EndDate = dto.EndDate;
                    plan.Progress = Math.Clamp(dto.Progress, 0, 100);
                }
                else
                {
                    plan = dto.ToEntity();
                    plan.CreatedAt = DateTime.Now;
                    plan.IdUserCreate = await _permitsService.IdUser();
                    plan.Progress = Math.Clamp(plan.Progress, 0, 100);
                    _context.GanttPlans.Add(plan);
                }

                await _context.SaveChangesAsync();
                return new APIResponseMessage<GanttPlanDTO>
                {
                    State = true,
                    Data = plan.ToDTO(),
                    Message = "Piano Gantt salvato",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPlansService), nameof(SaveAsync), EventsTypes.Error, ex);
                return Fail("Errore nel salvataggio del piano Gantt", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<APIResponseMessage<GanttPlanDTO>> DeleteAsync(int id)
        {
            try
            {
                var plan = await _context.GanttPlans.FirstOrDefaultAsync(x => x.Id == id);
                if (plan == null)
                    return Fail("Piano Gantt non trovato", HttpStatusCode.NotFound);

                _context.GanttPlans.Remove(plan);
                await _context.SaveChangesAsync();

                return new APIResponseMessage<GanttPlanDTO>
                {
                    State = true,
                    Message = "Piano Gantt eliminato",
                    Code = HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(GanttPlansService), nameof(DeleteAsync), EventsTypes.Error, ex);
                return Fail("Errore nell'eliminazione del piano Gantt", HttpStatusCode.InternalServerError);
            }
        }

        private static APIResponseMessage<GanttPlanDTO> Fail(string message, HttpStatusCode code)
            => new() { State = false, Message = message, Code = code };
    }
}
