using CRM.Client.Pages.TicketChats;
using CRM.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CRM.Server
{
    public class SignalRHub: Hub
    {
        private readonly CRM.Server.Services.MaintenanceState _maintenanceState;

        public SignalRHub(CRM.Server.Services.MaintenanceState maintenanceState)
        {
            _maintenanceState = maintenanceState;
        }

        public static Dictionary<string, string> Connections { get { return _connections.ToDictionary(c => c.Key, c => c.Value); } }

        private static ConcurrentDictionary<string,string> _connections = new ConcurrentDictionary<string,string>();

        public async Task SendAllMessageAsync(int IdTicket, int idChat)
        {
            
            await Clients.All.SendAsync("ReceiveMessage", IdTicket, idChat);
        }

        public  async Task SendMessageAsync(string IdUser, int idTicket, int idChat)
        {
            if (_connections.TryGetValue(IdUser, out var connId))
            {
                await Clients.Client(connId).SendAsync("ReceiveMessage", idTicket, idChat);
            }
        }


        public override async Task OnConnectedAsync()
        {
            var name = Context.User?.Identity?.Name;
            if (name != null)
            {
                _connections.TryAdd(name, Context.ConnectionId);
            }

            var maintenance = _maintenanceState.GetCurrent();
            if (maintenance.Active)
                await Clients.Caller.SendAsync("MaintenanceNotice", maintenance);

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var name = Context.User?.Identity?.Name;
            if (name != null)
            {
                _connections.TryRemove(name, out _);
            }
            return base.OnDisconnectedAsync(exception);
        }


    }

    public interface IHubCrm
    {
        Task SendMessageAsync(int IdTicket, int idChat);
        Task SendMessageAsync(string IdUser, int idTicket, int idChat);
    }
}
