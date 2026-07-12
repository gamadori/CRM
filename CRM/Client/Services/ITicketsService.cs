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
        Task<bool> CloseTicket(int id, TicketClose ticket);

        Task<bool> ReOpenTicket(int id, Ticket item);

        Task<PagingResponse<TicketDTO>> Search(TicketFilter args);

        Task<List<TicketScheduleItemDTO>> GetScheduleItemsAsync(TicketFilter args);

        Task<bool> UpdateScheduleAsync(int idTicket, TicketScheduleUpdateRequest request);

        Task<bool> StartProcessingAsync(int idTicket);

        Task<SemanticSearchResponse> SemanticSearch(SemanticSearchRequest request);

        Task<AssistantChatResponse> AssistantChat(AssistantChatRequest request);

        Task AssistantChatStream(
            AssistantChatRequest request,
            Action<List<TicketSimilarityResult>> onTickets,
            Action<string> onChunk,
            Action<string> onError,
            Action<int>? onLogId = null);

        /// <summary>Invia il voto di feedback dell'operatore su una risposta dell'assistente.</summary>
        Task<bool> SendAssistantFeedback(AssistantFeedbackRequest request);

        /// <summary>Elenco dei log Q&A dell'assistente (consultazione admin).</summary>
        Task<List<AssistantChatLogDTO>> GetAssistantLogs(AssistantChatLogFilter filter);

        Task<AssistantChatResponse> DataAssistantAsk(AssistantChatRequest request);

        Task<TicketSummaryProposalResponse> ProposeSummary(int idTicket, TicketSummaryProposalRequest request);

        Task<TicketDTO?> UpdateSummary(int idTicket, UpdateTicketSummaryRequest request);

        Task<HashSet<string>?> LoadAssignedUsers(int IdTicket);

        Task<HttpResponseMessage> AssignUsers(int idTicket, AssignUsersRequest Users);
    }
}
