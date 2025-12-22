using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen.Blazor;
using Syncfusion.Blazor.Popups;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Projects
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
       

        [Inject]
        IAGRestClientService RestClientService { get; set; }       

        [Inject]
        private IUserService _serviceUsers { get; set; }
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private Project _project = null;

        private List<Product> _products;

        private List<Company> _companies;

        private List<ApplicationUser> _users;


        private bool _dialogVisible = false;

        private SfDialog _dialogPopUp;

        private RadzenDropDown<int?> _ddCompany;
        private RadzenDropDown<string?> _ddUsers;

        protected override async Task OnInitializedAsync()
        {
           
            try
            {

                await LoadCompanies();
                await LoadProducts();
                await LoadUsers();

                if (Id != null)
                    _project = await RestClientService.GetItem<Project, int>(Id.Value, ConstHelper.ProjectsPath);
                else
                    _project = new Project() { State = (int)ProjectStates.Stop, StartDate = DateTime.Now };
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {

            try
            {
                _project = (await RestClientService.Post<Project, int>(_project, ConstHelper.ProjectsPath))?.Data;
                
                if (OnClickSave != null)
                    OnClickSave();
                else
                    NavigationManager.NavigateTo("/Projects/Index");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Projects/Index");

           
        }

        private async Task LoadCompanies()
        {
            
            var items = await RestClientService.Get<Company>(ConstHelper.CompaniesPath);

            if (items != null)
            {
                _companies = items;
            }
        }

        private async Task LoadProducts()
        {
            
            var resp = await RestClientService.Get<Product>(ConstHelper.Products); 

            if (resp != null)
            {
                _products = resp;
            }
        }

        private async Task LoadUsers()
        {
            var resp = await _serviceUsers.Get<ApplicationUser>(ConstHelper.UsersPath);

            if (resp != null)
            {
                _users = resp;
            }
        }
        protected void AddProductHandle()
        {
            _dialogVisible = true;
        }

        protected async void ProductOnClickSave()
        {
          
            StateHasChanged();
                 
        }

        protected void ProductOnClickCancel()
        {
            _dialogVisible = false;
            StateHasChanged();
        }

        private async Task OnGetCompany(int? id)
        {
            if (id != null)
            {
                await LoadCompanies();
                await _ddCompany.SelectItem(id, true);

            }
        }

        private async Task OnGetUser(string? id)
        {
            if (id != null)
            {
                await LoadUsers();
                await _ddUsers.SelectItem(id, true);

            }
        }

    }
}
