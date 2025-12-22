using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;


namespace CRM.Client.Pages.UsersGroups
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        private IManyToManyService<UserGroupModel> _serviceGroupUsers { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        [Parameter]
        public int IdGroup { get; set; }

        private IQueryable<ApplicationUser> _users = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private UsersFilterModel _filter = new UsersFilterModel();

        private string _messageDelete = "";


        private Group _group;

        private ApplicationUser _user;


        protected override async Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif
            await LoadGroup();
            await LoadData();
            
        }

        private async Task LoadGroup()
        {
            _group = await RestClientService.GetItem<Group, int>(IdGroup, ConstHelper.GroupsPath);
        }

        public async Task<IEnumerable<ApplicationUser>> LoadData()
        {
            var user = Enumerable.Empty<ApplicationUser>().AsQueryable();
            try
            {
                _filter.IdGroup = IdGroup;
                var pagingResponse = await _serviceUser.Get(_filter);

                _users = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

                user = _users;
                
                return user;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return user;
            }
            finally
            {
                
            }
     
        }

      

        protected async Task SearchSubmit()
        {
            if (_group != null)
                await LoadData();
        }

        protected void Edit(int id)
        {
            NavigationManager.NavigateTo($"/UsersGroups/Edit/{id}");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo("/UsersGroups");
        }
       

        protected async Task Delete()
        {
           
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_group != null)
            {
                await _serviceGroupUsers.Delete(new UserGroupModel()
                {
                    IdGroup = _group.Id,
                    IdUser = _user.Id
                });
                

                await LoadData();
            }
        }

        protected void PrepareDelete(ApplicationUser item)
        {
            _user = item;
            _messageDelete = $"Eliminare l'utente {item.Name} dal gruppo {_group.Name}";
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

        }

        protected void PrepareAdd()
        {
            JSRuntime.InvokeVoidAsync("ShowModal", "modalAddUser");
        }
    }
}
