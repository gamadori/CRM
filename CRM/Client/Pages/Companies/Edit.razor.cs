using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        ICompaniesService _companiesService { get; set; }
        [Inject]
        HttpClient Http { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.Buttons> LocalizeBtn { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IEnumService EnumService { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IUserService UserService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [CascadingParameter]
        private Task<AuthenticationState>? authenticationState { get; set; }

        //[Inject]
        //AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdReseller { get; set; }

       
        [Parameter]
        public EventCallback<int?> OnSaving { get; set; }

        [Parameter]
        public EventCallback OnCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private Company _company = null;

        private RadzenFileInput<string> inputImage;

        private List<EnumField> _companyTypesFields;

        private List<CompanyDTO> _companies;

        private Company? _userCompany;

        private bool _hidden = false;

        private bool _resellerDisabled = true;

        private PageHeaderModel _pageHeader = new PageHeaderModel();
        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                await LoadUserCompany();
                await LoadCompanyTypes();
                await LoadCompanies();

                _resellerDisabled = (_userCompany.CompanyType == CompanyTypes.Customer || _userCompany.CompanyType == CompanyTypes.Reseller);

                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.CompaniesPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                    _company = await Http.GetFromJsonAsync<Company>(path);
                }
                else
                {
                    _company = new Company();

                    if (_userCompany.CompanyType == CompanyTypes.Reseller)
                    {
                        _company.IdReseller = _userCompany.Id;
                    }
                }
                //_pageHeader = HeaderService.Create("Companies", Id, _company?.RagioneSociale, true, ConstHelper.ClientCompaniesPath);
                _pageHeader = await HeaderService.Create(PageMode);
                DisplayComponent();
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        


        private void DisplayComponent()
        {
            if (PageMode == PageModality.Dialog)
                _hidden = true;
            else
                _hidden = false;

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {

            try
            {
                var resp = await CompaniesService.Post<Company, int>(_company, ConstHelper.CompaniesPath);

                if (resp != null && resp.Data != null)
                {
                    _company = resp.Data;
                }

                if (PageMode == PageModality.Dialog)
                {
                    DialogService.CloseSide(_company.Id);
                }
                else if (OnSaving.HasDelegate)
                    await OnSaving.InvokeAsync();
                else
                    NavigationManager.NavigateTo("/Companies/Index");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected async Task Annulla()
        {
            if (OnCancel.HasDelegate)
                await OnCancel.InvokeAsync();
            else
                NavigationManager.NavigateTo("/Companies/Index");
        }


        private async Task LoadUserCompany()
        {
            _userCompany = await _companiesService.GetCompany();
        }
        private async Task LoadCompanyTypes()
        {
            List<int>? mask = null;
            
            if (!await UserService.CheckPolicy(ePolicy.AdminRole))
            {
                mask = new List<int>();
                mask.Add((int)CompanyTypes.HeadCompany);
            }
            _companyTypesFields = EnumService.EnumGetList(typeof(CompanyTypes), mask);
        }

        protected async Task LoadCompanies(LoadDataArgs args = null)
        {
            CompanyFilter data = new CompanyFilter();

            data.Reseller = true;

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                data.RagioneSociale = args.Filter;
            }
            var resp = await _companiesService.GetListPag<CompanyFilter, CompanyDTO>(data, ConstHelper.CompaniesPath); 

            _companies = resp?.Items;

            //StateHasChanged();
        }

       
        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }


    }
}
