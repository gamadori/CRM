using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ITicketSummaryService
    {
        Task<TicketSummaryProposalResponse?> ProposeAsync(int idTicket, TicketSummaryProposalRequest request);
        Task<TicketDTO?> UpdateAsync(int idTicket, UpdateTicketSummaryRequest request);
    }
}
