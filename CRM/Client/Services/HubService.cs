using CRM.Client.Pages.Tickets;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class HubService: IHubService
    {
        private readonly NavigationManager _navigationManager;

        private readonly AuthenticationStateProvider _authenticationStateProvider;

        public HubService(NavigationManager navigationManager, AuthenticationStateProvider authenticationStateProvider) 
        { 
            _navigationManager = navigationManager;
            _authenticationStateProvider = authenticationStateProvider;
        }


       
        public EventHandler<int> ReceveMessageEvent { get;  set; }

        public EventHandler<int> NewTicketEvent { get; set; }


        private HubConnection _hubConnection;

        private string _currentUserId;

        public async Task InitSignalR()
        {
            await GetCurrentUser();

            Uri url = _navigationManager.ToAbsoluteUri("/signalRHub");
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url)
                .WithAutomaticReconnect()
                .Build();
           
            await RegisterActions();
            
        }

        
        public async Task Close()
        {
       
            if (_hubConnection is not null)
                await _hubConnection.DisposeAsync();
        }
        private async Task RegisterActions()
        {
            _hubConnection.On<HubMessage>("ReceiveMessage", (msg) =>
            {
                if (msg != null && msg.IdUserTo != _currentUserId)
                    ReceveMessageEvent?.Invoke(this, msg.IdObject);
            });

            _hubConnection.On<HubMessage>("NewTicket", (msg) =>
            {
                if (msg != null && msg.IdUserTo == _currentUserId)
                    NewTicketEvent?.Invoke(this, msg.IdObject);
            });

            await _hubConnection.StartAsync();
        }

        private async Task GetCurrentUser()
        {
            var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            
            if (user.Identity.IsAuthenticated)
            {

                _currentUserId = user.Claims.Where(a => a.Type == "sub").Select(a => a.Value).FirstOrDefault();
            }
            else
            {
                _currentUserId = null;
            }
        }


    }
}
