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

        public static Dictionary<string, string> Connections
        {
            get
            {
                return _connections.ToDictionary(
                    c => c.Key,
                    c => c.Value.Keys.FirstOrDefault() ?? string.Empty);
            }
        }

        public static int ConnectedUsersCount => _connections.Count;

        public static int ConnectedConnectionsCount => _connections.Sum(c => c.Value.Count);

        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connections = new();

        public async Task SendAllMessageAsync(int IdTicket, int idChat)
        {
            
            await Clients.All.SendAsync("ReceiveMessage", IdTicket, idChat);
        }

        public  async Task SendMessageAsync(string IdUser, int idTicket, int idChat)
        {
            if (_connections.TryGetValue(IdUser, out var userConnections))
            {
                foreach (var connId in userConnections.Keys)
                {
                    await Clients.Client(connId).SendAsync("ReceiveMessage", idTicket, idChat);
                }
            }
        }


        public override async Task OnConnectedAsync()
        {
            var name = Context.User?.Identity?.Name;
            if (name != null)
            {
                var userConnections = _connections.GetOrAdd(name, _ => new ConcurrentDictionary<string, byte>());
                userConnections.TryAdd(Context.ConnectionId, 0);
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
                if (_connections.TryGetValue(name, out var userConnections))
                {
                    userConnections.TryRemove(Context.ConnectionId, out _);

                    if (userConnections.IsEmpty)
                        _connections.TryRemove(name, out _);
                }
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
