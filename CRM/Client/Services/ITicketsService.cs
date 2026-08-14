using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketsService : IRestClientModelService<Ticket, TicketDTO, TicketFilter, int>
    {
        Task<CloseTicketResponse> CloseTicket(int id, TicketClose ticket);

        /// <summary>
        /// Cancella riportando il motivo del rifiuto. La <c>Delete</c> generica restituisce un
        /// <c>bool</c> che nessuno guardava: la riga restava in elenco senza spiegazioni.
        /// </summary>
        Task<CloseTicketResponse> DeleteTicket(int id);

        /// <summary>Cosa impedisce la chiusura secondo il server: la UI non riderivano la regola.</summary>
        Task<TicketClosePreconditionDTO?> GetClosePreconditionAsync(int idTicket);

        Task<bool> ReOpenTicket(int id, Ticket item);

        Task<PagingResponse<TicketDTO>> Search(TicketFilter args);

        Task<List<TicketScheduleItemDTO>> GetScheduleItemsAsync(TicketFilter args);

        Task<TicketScheduleUpdateResult> UpdateScheduleAsync(int idTicket, TicketScheduleUpdateRequest request);

        /// <summary>I lavori dell'utente corrente, gia' raggruppati e ordinati dal server.</summary>
        Task<WorkListDTO> GetWorkListAsync();

        Task<SemanticSearchResponse> SemanticSearch(SemanticSearchRequest request);

        Task<TicketSummaryProposalResponse> ProposeSummary(int idTicket, TicketSummaryProposalRequest request);

        Task<TicketDTO?> UpdateSummary(int idTicket, UpdateTicketSummaryRequest request);

        Task<HashSet<string>?> LoadAssignedUsers(int IdTicket);

        /// <summary>Chi e' assegnato al ticket e a quale gruppo e' smistato: contesto del picker utenti.</summary>
        Task<TicketAssignmentContextDTO?> GetAssignmentContextAsync(int idTicket);

        Task<HttpResponseMessage> AssignUsers(int idTicket, AssignUsersRequest Users);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ClaimAsync(int idTicket);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> BlockAsync(int idTicket, TicketBlockRequest request);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> UnblockAsync(int idTicket, TicketUnblockRequest request);

        /// <summary>Assegna il gruppo proposto dallo smistamento AI.</summary>
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> AcceptAiRoutingAsync(int idTicket);

        /// <summary>Scarta il suggerimento dello smistamento AI.</summary>
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> DismissAiRoutingAsync(int idTicket);
    }
}
