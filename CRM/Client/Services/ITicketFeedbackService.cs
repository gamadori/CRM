using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Client.Services
{
    /// <summary>
    /// Interfaccia per la gestione dei feedback dei ticket.
    /// Implementata da:
    /// - Server: TicketFeedbackService (accesso diretto al DB)
    /// - Client: ProxyTicketFeedbackService (chiamate HTTP)
    /// </summary>
    public interface ITicketFeedbackService : IDataService<TicketFeedback, TicketFeedbackResponse, int, TicketFeedbackFilterModel, object>
    {
        /// <summary>
        /// Ottiene i ticket chiusi in attesa di feedback per l'utente corrente
        /// </summary>
        Task<List<TicketPendingFeedback>> GetPendingFeedbacksAsync();

        /// <summary>
        /// Ottiene il conteggio dei ticket in attesa di feedback
        /// </summary>
        Task<int> GetPendingFeedbacksCountAsync();

        /// <summary>
        /// Crea un nuovo feedback per un ticket
        /// </summary>
        Task<TicketFeedbackResponse> CreateFeedbackAsync(TicketFeedbackRequest request);

        /// <summary>
        /// Ottiene un feedback specifico per ID
        /// </summary>
        Task<TicketFeedbackResponse?> GetFeedbackAsync(int id);

        /// <summary>
        /// Ottiene il feedback di un ticket specifico
        /// </summary>
        Task<TicketFeedbackResponse?> GetFeedbackByTicketAsync(int ticketId);

        /// <summary>
        /// Salta il feedback per un ticket
        /// </summary>
        Task<bool> SkipFeedbackAsync(int ticketId);

        /// <summary>
        /// Ottiene tutti i feedback (solo admin)
        /// </summary>
        Task<List<TicketFeedbackResponse>> GetAllFeedbacksAsync(bool unreadOnly = false);

        /// <summary>
        /// Segna un feedback come letto (solo admin)
        /// </summary>
        Task<bool> MarkAsReadAsync(int id);

        /// <summary>
        /// Ottiene la valutazione media dei feedback
        /// </summary>
        Task<AverageFeedbackDTO> AverageRateAsync();
    }
}
