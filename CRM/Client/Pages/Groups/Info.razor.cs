using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Groups
{
    public partial class Info : ComponentBase, IDisposable
    {
        public enum GroupViews
        {
            Group,
            Users
        }

        public enum PartialViews
        {
            Index,
            Details,
            Edit,
            New

        }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IManyToManyService<UserGroupModel> _userGroupService { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        Radzen.DialogService dialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        private GroupViews selectView = GroupViews.Group;

        private PartialViews _partialView = PartialViews.Details;

        private string _idUser;

        private Group _group = null;

        private bool _fromDetails = false;

        private string _messagePrepareDelete = "Eliminare l'utente {0} dal Gruppo";

        private Settings.Users.Index _pageUsersIndex;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        

        protected override async Task OnInitializedAsync()
        {
            dialogService.OnClose += async (s) => await OnClickClose(s);

            await LoadGroup();

            _pageHeader = await HeaderService.Create();
        }

        public void Dispose()
        {
            // The DialogService is a singleton so it is advisable to unsubscribe.
            //dialogService.OnOpen -= Open;
            dialogService.OnClose -= async (s) => await OnClickClose(s);
        }

        private async Task LoadGroup()
        {
            _group = await RestClientService.GetItem<Group, int>(Id, ConstHelper.GroupsPath);
        }
        private void EditGroup()
        {
            selectView = GroupViews.Group;
            _partialView = PartialViews.Edit;

            StateHasChanged();
        }


        void CancelGroup()
        {
            selectView = GroupViews.Group;
            _partialView = PartialViews.Details;

            StateHasChanged();
        }


        private async Task SaveGroup()
        {
            selectView = GroupViews.Group;
            _partialView = PartialViews.Details;
            await LoadGroup();
            StateHasChanged();
        }
        private void EditUser(string id)
        {
            selectView = GroupViews.Users;
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
            selectView = GroupViews.Users;
            _partialView = PartialViews.Details;
            _idUser = id;

            StateHasChanged();
        }

        private void UsersCloseForm()
        {
            selectView = GroupViews.Users;

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

            if (selectView == GroupViews.Group)
                _partialView = PartialViews.Details;
            else
            _partialView = PartialViews.Index;

            StateHasChanged();
        }

        protected async Task OnClickNew(string id)
        {
            await dialogService.OpenAsync<AddUser>($"Gruppo {_group.Name}",
                        new Dictionary<string, object>() { { "IdGroup", Id } },
                        new Radzen.DialogOptions() { Width = "700px", Height = "530px" });

            
            
        }

        protected async Task OnClickDelete(string idUser)
        {

            await _userGroupService.Delete(new UserGroupModel() { IdGroup = Id, IdUser = idUser }  );
            dialogService.Close();
            await _pageUsersIndex?.InitPage();
            await LoadGroup();
            StateHasChanged();
        }

        protected async Task OnClickClose(dynamic result)
        {
            //dialogService.Close();
           // await _pageUsersIndex?.InitPage();
            StateHasChanged();
        }

       
    }
}
