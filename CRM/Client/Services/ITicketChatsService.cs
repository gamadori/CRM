using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ITicketChatsService : IRestClientModelService<TicketChat, TicketChatViewModel, TicketChatFilterModel, int>
    {
        Task<bool> ChatRead(int idChat, TicketChatViewModel item);

        Task<bool> HasNewMessage(int idTicket);

    }
}
