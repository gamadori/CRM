namespace CRM.Server.Services
{
    public interface ITicketsService
    {
        Task<List<(string?, string?)>> GetEmails(int idTicket);
    }
}
