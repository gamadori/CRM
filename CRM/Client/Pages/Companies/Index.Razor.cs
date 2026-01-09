using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Companies
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject] 
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IRestService<ApplicationUser> userSigned { get; set; }

        [Inject]
        DialogService dialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? IdReseller { get; set; } = null;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public int? IdCompanyParent { get; set; } = null;

        
        [Parameter]
        public EventCallback<int?> OnSelectCompany { get; set; }

       
        [Parameter]
        public EventCallback<int> OnAddCustomer { get; set; }    


        [Parameter]
        public EventCallback<int> OnEditCompany { get; set; }

        [Parameter]
        public EventCallback<int> OnRemoveCompany { get; set; }

        [Parameter]
        public EventCallback OnAddNewItem { get; set; }


        private IQueryable<Company> _companies = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private CompanyFilter _filter = new CompanyFilter() {  PageSize = 10, Skip = 0, Top = 10 };

        private string _pageMessge = "";

        private string _messageDelete = "";

        private ApplicationUser? _user;

        private Company _company;

        private string pagingSummaryFormat;

        private int _companyPageSize = 10;

        private bool _isLoading = false;

        private RadzenDataGrid<Company> grdCompanies;

        private FilterMode _filterMode = FilterMode.Advanced;

        private string _search = string.Empty;

        private string _header;

        private PageHeaderModel? _pageHeader = null;


        protected override async Task OnInitializedAsync()
        {
            if (IdReseller != null)
            {
                _header = Localize["Companies"];
            }
            else
                _header = Localize["Customers"];

            

            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            _user = await userSigned.Get();


            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();

            //_pageHeader = HeaderService.Create(ConstHelper.ClientCompaniesPath, null, null, false, ConstHelper.ClientCompaniesPath, null, PageMode);
            _pageHeader = await HeaderService.Create(PageMode);
        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            try
            {
                await GetCompanies(args);
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
            }
            finally
            {
                if (_companies == null)
                    _companies = Enumerable.Empty<Company>().AsQueryable();

                StateHasChanged();
                
            }
     
        }

       

      

        public async Task GetCompanies(LoadDataArgs args = null)
        {
            try                
            {

               

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;

                    _filter.OrderBy = args?.OrderBy;
                    _filter.Filter = args?.Filter;
               
                }

                if (_search.Length > 0)
                {
                    _filter.RagioneSociale = _search;
                }
                else
                    _filter.RagioneSociale = null;

                _filter.IdReseller = IdReseller;

                if (PageMode == PageModality.Dialog)
                    _filter.IdCompanyParent = IdCompanyParent;

                PagingResponse<Company> pagingResponse = await RestClientService.Get<Company, CompanyFilter>(_filter, ConstHelper.CompaniesPath);

                if (pagingResponse != null)
                {
                    _companies = pagingResponse.Items.AsQueryable();
                    _paging = pagingResponse.MetaData;
                }
                else
                    _pageMessge = "Errore";

                

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {
                
                _pageMessge = ex.Message;
                
            }

            catch (Exception ex)
            {
                _pageMessge = ex.Message;
                
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }


        protected async Task Details(int id)
        {
            if (OnSelectCompany.HasDelegate)
                await OnSelectCompany.InvokeAsync(id);
            else
                NavigationManager.NavigateTo($"/Companies/{id}");
        }

        protected async void Edit(int id)
        {
            if (OnEditCompany.HasDelegate)
                await OnEditCompany.InvokeAsync(id);
            else
                NavigationManager.NavigateTo($"/Companies/{id}/Edit");
        }
        protected async Task NewCompany()
        {
            if (OnAddNewItem.HasDelegate)
            {

                await OnAddNewItem.InvokeAsync();
            }
            else
                NavigationManager.NavigateTo("/Companies/New");
        }

     

        protected async Task Delete(Company company)
        {
            if (OnRemoveCompany.HasDelegate)
            {
                await OnRemoveCompany.InvokeAsync(company.Id);
                await LoadData();
            }
            else
            {
                if (await dialogService.Confirm($"{Localize["Eliminare definitivamente l'azienda"]}: {company.RagioneSociale}") == true)
                {
                    await RestClientService.Delete<int>(company.Id, ConstHelper.CompaniesPath);

                    await LoadData();
                }

            }
        }

        protected async void OnChangeRagioneSocialeFilter(ChangeEventArgs args)
        {
            _filter.RagioneSociale = args.Value.ToString();
            await grdCompanies.GoToPage(0);
            await LoadData();
        }

        protected async void OnChangeStatoFilter(ChangeEventArgs args)
        {
            _filter.Stato = args.Value.ToString();
            await grdCompanies.GoToPage(0);
            await LoadData();
        }

        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.RagioneSociale = "";
                _filter.Stato = "";

                
            }
            await LoadData();
        }

        protected void ImportData()
        {
            NavigationManager.NavigateTo($"/CSVSettings/CSVData/{CSVTable.Company.ToString()}");
        }

        private async Task OnClickName(int? id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnSelectCompany.HasDelegate)
                    {
                        await OnSelectCompany.InvokeAsync(id);
                    }
                    else
                        dialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:

                    if (OnSelectCompany.HasDelegate)
                    {

                        await OnSelectCompany.InvokeAsync(id);
                    }
                    else
                        NavigationManager.NavigateTo($"/Companies/{id}");
                    
                    break;
            }
            
        }

        private async void FilterChanged(string? value)
        {

            _search = value;
            await LoadData();
            StateHasChanged();
        }

        private async Task OnGetCustomer(int? id)
        {
            if (id != null)
            {
                if (OnAddCustomer.HasDelegate)
                {
                    await OnAddCustomer.InvokeAsync((int)id);
                    await LoadData();
                }
            }
        }
    }
}
