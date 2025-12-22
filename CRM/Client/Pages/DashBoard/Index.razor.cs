using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.SignalR.Client;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using MediatR;
using static CRM.Client.Program;

namespace CRM.Client.Pages.DashBoard
{
    [Authorize(Policy = "StandardRole")]
    
    public partial class Index : ComponentBase, INotificationHandler<MsgNotify>, IDisposable
    {

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IReportService<TicketDashBoardModel, TicketDashBoardModelFilter> _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        
        
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService dialogService { get; set; }

        [Inject]
        AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        private TicketDashBoardModel _model = null;
        private string _currentUserId = null;

        private string _userId;

        private List<ApplicationUser> _users;

        protected override async Task OnInitializedAsync()
        {

            
            DynamicNotificationHandlers.Register(this);
            //_user = await _userService.Get();

            await GetCurrentUser();
            _userId = _currentUserId;
            await LoadUsers();
            await LoadData();
            
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            await LoadData();

        }

        private async Task LoadData()
        {
            TicketDashBoardModelFilter filter = new TicketDashBoardModelFilter();
            filter.IdUser = _userId;
           _model = await _service.Get(filter);
            StateHasChanged();
        }
        protected void AddTicket()
        {
            NavigationManager.NavigateTo("/Tickets/Create");
        }

        protected void TicketWorking()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.Working}"));
          
        }

        protected void TicketExpired()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.Expired}"));
        }

        protected void TicketNotAssigned()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.NotAssigned}");
        }

        protected void TicketsNewMessage()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.NewMessage}"));
        }


        protected void TicketAll()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.All}"));
        }

        protected void Schedule()
        {
            NavigationManager.NavigateTo("/Tickets/Schedule");
        }

        protected void TicketsSearch()
        {
            NavigationManager.NavigateTo("/Tickets/Search");
        }

        protected void TicketsToInvoice()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.ToBeInvoiced}");
        }


        protected void UsersNeedConfirm()
        {
            NavigationManager.NavigateTo($"/Settings/Users/true");
        }

        protected async Task OnClickNew1()
        {
            await dialogService.Confirm($"{Localize["Confermare l'utente"]}", Localize["Conferma Utente"], null);

            //dialogService.OnClose += async (s) => await OnClickClose(s);
        }

        private async Task GetCurrentUser()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
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

        private async Task<List<ApplicationUser>> LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();

           

            var response = await _serviceUser.GetList(request);

            _users = response.Items.ToList();

            return _users;
        }

        protected async void OnChangeIdUser()
        {
            //StateHasChanged();
            await LoadData();
        }

        public void Dispose()
        {

            DynamicNotificationHandlers.Unregister(this);
        }

        private string Url(string url)
        {
            try
            {
                if (_userId != null && _userId.Length > 0) 
                    return $"{url}/{_userId}";
                else
                    return url;
            }
            catch (Exception ex)
            {
                return url;
            }
        }
    }
}
