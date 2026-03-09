using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
   
    public interface ITicketsService
    {
        // Existing methods
        Task<List<(string?, string?)>> GetEmails(int idTicket);
        Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket);
        Task<List<UserModel>> GetUsersCanAssignTicketTypeAsync(int idType);

        // CRUD
        Task<Ticket?> GetItemAsync(int id);
        Task<TicketDTO?> GetDetailsAsync(int id);
        Task<ObjectView<TicketDTO, string>> GetPagingAsync(TicketFilter args);
        Task<Ticket> PostAsync(Ticket ticket);
        Task<bool> PutAsync(int id, Ticket ticket);
        Task<bool> DeleteAsync(int id);

        // Close / ReOpen
        Task<bool> CloseAsync(int id, TicketClose model);
        Task<bool> ReOpenAsync(int id);

        // State
        Task SetTicketStateAsync(TicketDTO ticket);
        Task SetTicketStateAsync(Ticket ticket);

        // Assignment
        Task<List<string>> GetAssignedUserIdsAsync(int idTicket);
        Task<AssignUsersResult> AssignUsersAsync(int idTicket, AssignUsersRequest request, string? currentUserId);

        // Helpers
        Task<bool> TicketChangeAssigned(int id, string? idAssigned);
        Task CheckTicketExpired(int id);
        Task<int> GetDayBeforeExpired(int id);
    }

    public class AssignUsersResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> AddedUserIds { get; set; } = new();
        public List<string> RemovedUserIds { get; set; } = new();
        public int AssignedCount { get; set; }
        public Ticket? Ticket { get; set; }
    }
}
