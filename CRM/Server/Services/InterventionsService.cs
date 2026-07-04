using CRM.Client.Services;   // ILogEventService
using CRM.Server.Data;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Lettura degli interventi tecnici, con controllo dei permessi tramite il ticket.
    /// </summary>
    public class InterventionsService : IInterventionsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public InterventionsService(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        public async Task<List<TicketInterventionSummaryDTO>> GetByTicketAsync(int idTicket)
        {
            try
            {
                // Permesso: se non può accedere al ticket, nessun intervento
                if (!await _permitsService.CanGetTicket(idTicket))
                    return new List<TicketInterventionSummaryDTO>();

                return await _context.TicketsInterventions.AsNoTracking()
                    .Where(i => i.IdTicket == idTicket)
                    .OrderByDescending(i => i.StartDateTime)
                    .Select(i => new TicketInterventionSummaryDTO
                    {
                        Id = i.Id,
                        IdTicket = i.IdTicket,
                        SupportType = i.SupportType,
                        Activities = i.Activities,
                        MountedParts = i.MountedParts,
                        Note = i.Note,
                        StartDateTime = i.StartDateTime,
                        EndDateTime = i.EndDateTime,
                        Minute = i.Minute
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(InterventionsService), nameof(GetByTicketAsync), EventsTypes.Error, ex);
                return new List<TicketInterventionSummaryDTO>();
            }
        }
    }
}
