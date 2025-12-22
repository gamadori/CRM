using System;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IHubService
    {
        EventHandler<int> ReceveMessageEvent { get; set; }

        EventHandler<int> NewTicketEvent { get; set; }

        Task InitSignalR();

        Task Close();
    }
}
