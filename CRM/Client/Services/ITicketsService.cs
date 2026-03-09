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

        Task<SemanticSearchResponse> SemanticSearch(SemanticSearchRequest request);

        Task<HashSet<string>?> LoadAssignedUsers(int IdTicket);

        Task<HttpResponseMessage> AssignUsers(int idTicket, AssignUsersRequest Users);
    }
}
