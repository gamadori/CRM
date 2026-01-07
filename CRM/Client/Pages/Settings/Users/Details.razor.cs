using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Settings.Users
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IUserService _userService { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }
        
        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        
        [Parameter]
        public string Id { get; set; }

        [Parameter]
        public Action CloseForm { get; set; }

        [Parameter]
        public Action<string> OnClickEdit { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private Func<Task> ConfirmUser { get; set; }



        private ApplicationUser _user = null;


        private bool readOnly = false;

        private Company _company = new Company();

        private string _message;

       
        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {

                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.UsersPath;

                
                if (readOnly = (Id != null))
                {
                    path += $"/{Id}";
                    
                    _user = await Http.GetFromJsonAsync<ApplicationUser>(path);
                    
                }
                else
                    _user = new ApplicationUser();

                await LoadCompany();

               
                //_pageHeader = HeaderService.Create("Users", Id, _user?.UserName, false, ConstHelper.ClientUsersPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

     

        protected void Annulla()
        {
            if (CloseForm != null)
                CloseForm();
            else
                NavigationManager.NavigateTo("/Settings/Users/Index");
        }

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit(Id);
            else
                NavigationManager.NavigateTo($"/Settings/Users/{Id}/Edit");
        }

        protected void Roles()
        {
           
            NavigationManager.NavigateTo($"/Settings/Users/{Id}/Roles");
        }

        protected void PreparateConfirm()
        {
            ConfirmUser = Confirm;
            _message = $"Confermare l'utente {_user.NameComplete}?";
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
        }
        protected async Task Confirm()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");
            _user = await _userService.Confirm(_user.Id);
            
            StateHasChanged();
        }

        protected void PrepareSendInvite()
        {
            ConfirmUser = SendInvite;
            _message = $"Reinviare l'invito all'utente {_user.NameComplete}?";
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");
        }

        protected async Task SendInvite()
        {
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");
            await _userService.SendInvite(_user.Id);

            StateHasChanged();
        }

        protected async Task LoadCompany()
        {
            if (_user.IdCompany != null)
                _company = await RestClientService.GetItem<Company, int>(_user.IdCompany.Value, ConstHelper.CompaniesPath);


        }
    }
}
