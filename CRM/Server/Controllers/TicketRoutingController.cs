using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Server.Services.TicketRouting;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Configurazione dello smistamento automatico dei ticket: parametri, competenze dei gruppi
    /// e prova a vuoto. Sta fuori dalle impostazioni globali perche' e' una funzione a se' stante,
    /// con una pagina dedicata in cui si tara e si controlla come sta andando.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "SuperUserRole")]
    public class TicketRoutingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ITicketRoutingService _routing;
        private readonly IPermitsService _permits;
        private readonly ILogEventService _logEventService;

        public TicketRoutingController(
            ApplicationDbContext context,
            ITicketRoutingService routing,
            IPermitsService permits,
            ILogEventService logEventService)
        {
            _context = context;
            _routing = routing;
            _permits = permits;
            _logEventService = logEventService;
        }

        [HttpGet("settings")]
        public async Task<ActionResult<TicketRoutingSetting>> GetSettings()
            => await _routing.GetSettingsAsync(HttpContext.RequestAborted);

        [HttpPut("settings")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<TicketRoutingSetting>> PutSettings(TicketRoutingSetting model)
        {
            try
            {
                var settings = await _routing.GetSettingsAsync(HttpContext.RequestAborted);

                settings.Enabled = model.Enabled;
                settings.AutoAssignThreshold = Math.Clamp(model.AutoAssignThreshold, 0.5, 1.0);
                settings.RestrictToTicketTypeGroups = model.RestrictToTicketTypeGroups;
                settings.IdFallbackGroup = model.IdFallbackGroup;
                settings.ApplyToEmailTickets = model.ApplyToEmailTickets;
                settings.NotifyGroupOnAssign = model.NotifyGroupOnAssign;
                settings.Model = string.IsNullOrWhiteSpace(model.Model) ? null : model.Model!.Trim();
                settings.UpdatedAt = DateTime.Now;
                settings.UpdatedBy = await _permits.IdUser();

                await _context.SaveChangesAsync(HttpContext.RequestAborted);

                return settings;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketRoutingController), nameof(PutSettings), EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        [HttpGet("status")]
        public async Task<ActionResult<TicketRoutingStatusDTO>> GetStatus()
            => await _routing.GetStatusAsync(HttpContext.RequestAborted);

        /// <summary>Gruppi con le competenze dichiarate: e' la conoscenza su cui l'AI decide.</summary>
        [HttpGet("groups")]
        public async Task<ActionResult<List<TicketRoutingGroupDTO>>> GetGroups()
        {
            return await _context.Groups
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new TicketRoutingGroupDTO
                {
                    Id = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    AiRoutingHints = g.AiRoutingHints,
                    UsersCount = g.Users.Count(u => !u.IsDeleted),
                    TicketTypes = g.TicketTypes.OrderBy(t => t.Desc).Select(t => t.Desc).ToList()
                })
                .ToListAsync(HttpContext.RequestAborted);
        }

        [HttpPut("groups/{id}/hints")]
        [Authorize(Policy = "AdminRole")]
        public async Task<IActionResult> PutGroupHints(int id, TicketRoutingHintsRequest request)
        {
            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == id, HttpContext.RequestAborted);

            if (group == null)
                return NotFound();

            group.AiRoutingHints = string.IsNullOrWhiteSpace(request.AiRoutingHints) ? null : request.AiRoutingHints!.Trim();
            await _context.SaveChangesAsync(HttpContext.RequestAborted);

            return NoContent();
        }

        /// <summary>Prova lo smistamento su un testo, senza creare ticket ne' modificare nulla.</summary>
        [HttpPost("preview")]
        public async Task<ActionResult<TicketRoutingPreviewResult>> Preview(TicketRoutingPreviewRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            return await _routing.PreviewAsync(request, HttpContext.RequestAborted);
        }
    }
}
