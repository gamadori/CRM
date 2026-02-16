using CRM.Shared;

namespace CRM.Server.Services
{
    public interface ITicketsService
    {
        Task<List<(string?, string?)>> GetEmails(int idTicket);

        Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket);

        Task<List<UserModel>> GetUsersCanAssignTicketTypeAsync(int idType);

    }
}
