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

        Task<TicketSummaryProposalResponse> ProposeSummary(int idTicket, TicketSummaryProposalRequest request);

        Task<TicketDTO?> UpdateSummary(int idTicket, UpdateTicketSummaryRequest request);

        Task<HashSet<string>?> LoadAssignedUsers(int IdTicket);

        Task<HttpResponseMessage> AssignUsers(int idTicket, AssignUsersRequest Users);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ClaimAsync(int idTicket);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> BlockAsync(int idTicket, TicketBlockRequest request);

        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> UnblockAsync(int idTicket, TicketUnblockRequest request);
    }
}
