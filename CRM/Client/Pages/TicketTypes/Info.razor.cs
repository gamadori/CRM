using CRM.Client.Models;
using CRM.Client.Pages.Tickets;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketTypes
{
    public partial class Info : ComponentBase, IDisposable
    {
        public enum TicketTypeViews
        {
            Ticket,
            Users,
            Groups
        }

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New

        }

        [Inject]
        private ITicketTypesService _service { get; set; }

        [Inject]
        private IManyToManyService<TicketTypeUser> _usersService { get; set; }

        [Inject]
        private IManyToManyService<TicketTypeGroup> _groupService { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        DialogService dialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        protected TicketTypeViews _selectView = TicketTypeViews.Ticket;

        private PartialViews _partialView = PartialViews.Details;

        private string _idUser;

        private TicketType _ticketType = null;

        private bool _fromDetails = false;

        private string _messagePrepareDelete = "Eliminare l'utente {0} dal Tipo di Ticket";


      
        private Settings.Users.Index _pageUsersIndex;

        private Groups.Index _pageGroupsIndex;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            dialogService.OnClose += async (s) => await OnClickClose(s);
            await LoadTicketType();

            _pageHeader = await HeaderService.Create();
        }

        public void Dispose()
        {
            // The DialogService is a singleton so it is advisable to unsubscribe.
            //dialogService.OnOpen -= Open;
            dialogService.OnClose -= async (s) => await OnClickClose(s);
        }

        private async Task LoadTicketType()
        {
            _ticketType = await _service.Get(Id);
        }
        private void EditTicketType()
        {
            _selectView = TicketTypeViews.Ticket;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }


        void CancelTicketType()
        {
            _selectView = TicketTypeViews.Ticket;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveTicketType()
        {
            _selectView = TicketTypeViews.Ticket;
            _partialView = PartialViews.Details;
            await LoadTicketType();

            StateHasChanged();
        }
        private void EditUser(string id)
        {
            _selectView = TicketTypeViews.Users;
            _partialView = PartialViews.Edit;
            _idUser = id;

            StateHasChanged();
        }

        private void UsersEditUser(string id)
        {
            _fromDetails = false;
            EditUser(id);
        }

        private void DetailsEditUser(string id)
        {
            _fromDetails = true;
            EditUser(id);
        }
        
        private void DetailsUser(string id)
        {
            _selectView = TicketTypeViews.Users;
            _partialView = PartialViews.Details;
            _idUser = id;

            StateHasChanged();
        }

        private void UsersCloseForm()
        {
            _selectView = TicketTypeViews.Users;

            if (_fromDetails)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            _fromDetails = false;

            StateHasChanged();
        }

        void Change(object value, string name)
        {
            
            _fromDetails = false;

            if (_selectView == TicketTypeViews.Ticket)
                _partialView = PartialViews.Details;
            else
                _partialView = PartialViews.Index;

            StateHasChanged();
        }

        protected async Task OnClickNew(string id)
        {
           //dialogService.OnClose += async (s) => await OnClickClose(s);
            await dialogService.OpenAsync<AddUser>($"Tipo Ticket {_ticketType.Desc}",
                new Dictionary<string, object>() { { "IdTicket", Id } },
                new DialogOptions() {  Width= "700px", Height = "auto"});

            await _pageUsersIndex.InitPage();

            StateHasChanged();
        }

      

        

        protected async Task OnClickDelete(string idUser)
        {

            await _usersService.Delete(new TicketTypeUser() { IdTicket = Id, IdUser = idUser }  );
            dialogService.Close();

            await _pageUsersIndex.InitPage();
            StateHasChanged();
        }

        protected async Task OnClickGroupNew(int? id)
        {
            await dialogService.OpenAsync<AddGroup>($"Tipo Ticket {_ticketType.Desc}",
                new Dictionary<string, object>() { { "IdTicket", Id } },
                new DialogOptions() { Width = "700px", Height = "auto" });
            
            await _pageGroupsIndex.LoadData();

            StateHasChanged();
           
        }

        protected async Task OnClickGroupDelete(int idGroup)
        {
            await _groupService.Delete(new TicketTypeGroup() { IdTicket = Id, IdGroup = idGroup });
            await _pageUsersIndex.LoadData();

            StateHasChanged();
        }
        protected async Task OnClickClose(dynamic result)
        {
            //dialogService.Close();
            await LoadTicketType();
            StateHasChanged();
        }

       
    }
}
