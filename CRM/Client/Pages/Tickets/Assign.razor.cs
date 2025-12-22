using CRM.Client.Helpers;
using CRM.Client.Pages.Groups;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    public partial class Assign: ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        private ITicketService  _service { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

        [Inject]
        private IManyToManyService<UserGroupModel> _userGroupService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public EventCallback OnClose { get; set; }

        

        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private Ticket _ticket;

        private Radzen.Blazor.RadzenDropDownDataGrid<string> _userDropDown;




        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await LoadUsers(new LoadDataArgs());
        }

        private async Task LoadData()
        {
            _ticket = await _service.Get(Id);
        }
        public async Task LoadUsers(LoadDataArgs args)
        {
            UsersFilterModel request = new UsersFilterModel();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.NameComplete = args.Filter;
                
            }
            request.IdTicketToAssign = Id;
            request.PageSize = 0;
            var response = await _usersService.GetList(request);

            _users = response.Items;

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {


            try
            {
                

                
                _ticket = (await _service.Post(_ticket)).Data;

                
                if (OnClose.HasDelegate)
                    await OnClose.InvokeAsync();
                else
                    NavigationManager.NavigateTo($"/Tickets/{Id}");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected async void Cancel()
        {
            if (OnClose.HasDelegate)
                await OnClose.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Tickets/{Id}");
        }

        private async Task OpenScheduler()
        {
            dynamic result = await DialogService.OpenSideAsync<TicketCalendar>(Localize["Assign"],
                new Dictionary<string, object>() { { "Date", _ticket.Date ?? DateTime.Now }, {"IdUser", _ticket.IdUserAssigned }  , { "IdTicket", Id } },
                new SideDialogOptions { Position = DialogPosition.Top, ShowMask = false, Height = "auto", Style = "max-height: 90%;" });

            if (result != null)
            {
                _ticket.IdUserAssigned = ((SchedulerUserDate)result).IdUser;
                await LoadUsers(new LoadDataArgs());    
            }
            
            StateHasChanged();
        }

    }
}
