using CRM.Shared;
using Microsoft.AspNetCore.Components.Forms;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketChatsService : IRestClientModelService<TicketChat, TicketChatViewModel, TicketChatFilterModel, int>
    {
        Task<bool> ChatRead(int idChat, TicketChatViewModel item);
        Task<bool> TicketRead(int idTicket);
        Task<bool> HasNewMessage(int idTicket);
        Task<ChatFileUploadResult?> UploadFile(int idTicket, IBrowserFile file);
    }
}
