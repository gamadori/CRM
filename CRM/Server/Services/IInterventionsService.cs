using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio di lettura per gli interventi tecnici dei ticket.
    /// </summary>
    public interface IInterventionsService
    {
        /// <summary>
        /// Restituisce gli interventi di un ticket, ordinati dal più recente.
        /// Applica i permessi: se l'utente non può accedere al ticket, restituisce una lista vuota.
        /// </summary>
        Task<List<TicketInterventionSummaryDTO>> GetByTicketAsync(int idTicket);
    }
}
