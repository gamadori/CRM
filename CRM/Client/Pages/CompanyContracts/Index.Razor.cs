using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;
using static CRM.Client.Pages.Articles.Info;

namespace CRM.Client.Pages.CompanyContracts
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        public enum CompanyContractsViews
        {
            Active,
            Historic,
        }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private ICompanyContractsService Service { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IRestService<ApplicationUser> UserSignedService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Parameter]
        public int IdCompany { get; set; }

        [Parameter]
        public EventCallback OnNewItem { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnGotoIndex { get; set; }

        [Parameter]
        public EventCallback<int?> OnSelectItem { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter]
        public bool BreadCrumbVisible { get; set; } = true;

        private PagingResponse<CompanyContract> _companyContracts = null;

        private bool _isLoading = false;

        private RadzenDataGrid<CompanyContract> _grdProductTypeAccType;

        private string _header = "Company Contracts";

        private bool _filterState = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>() ;

        private ApplicationUser? _user;

        private FilterMode _filterMode = FilterMode.Advanced;


        private CompanyContractsViews _selectView = CompanyContractsViews.Active;
        protected async override Task OnInitializedAsync()
        {
            _bread = await BreadCrumbService.ProductTypeAccs(false);

            _header = Localize["Company Contracts\""];

            _user = await UserSignedService.Get();

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();
            
            StateHasChanged();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            CompanyContractFilter paging = new CompanyContractFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {
                paging.IdCompany = IdCompany;
                paging.Active = (_selectView == CompanyContractsViews.Active);

                if (args != null)
                {
                    paging.Skip = args.Skip;
                    paging.Top = args.Top;
                    paging.OrderBy = args.OrderBy;

                    if (args.Filters != null && args.Filters.Any())
                    {
                        if (paging.Filter?.Length > 0)
                            paging.Filter += " And ";

                        paging.Filter += args.Filter;
                    }
                }

                _companyContracts = await Service.Get(paging);
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_companyContracts == null)
                {
                    _companyContracts = new PagingResponse<CompanyContract>();

                    _companyContracts.Items = new List<CompanyContract>();
                    _companyContracts.MetaData = new PagingHeaderModel();

                }
                _isLoading = false;
            }

        }

       

        
        private void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
            {
                
                NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}/Details/{id}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}/Edit/{id}");
        }

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Eliminare il contratto selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await Service.Delete(id);

                    await LoadData();
                    StateHasChanged();
                }
            }
        }
        private async void NewItem()
        {
            if (OnNewItem.HasDelegate)
                await OnNewItem.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}/Edit");
        }


        protected async void OnChangeFilter(bool state)
        {

            
            await LoadData();
            StateHasChanged();
        }

        private async Task OnChangeIdUser()
        {
            await LoadData();
        }

       

        private async Task OnClickName(int? id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnSelectItem.HasDelegate)
                    {
                        await OnSelectItem.InvokeAsync(id);
                    }
                    else
                        DialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientCompanyContractsPath}/{id}");
                    break;
            }

        }

        private async Task Change()
        {
            await LoadData();
            StateHasChanged();
        }

        void CellRender(DataGridCellRenderEventArgs<CompanyContract> args)
        {
            if (args.Column.Property == nameof(CompanyContract.Active))
            {
                if (args.Data.Suspended)
                {
                    args.Attributes.Add("style", $"background-color: var(--rz-warning)");
                }
                else
                {
                    args.Attributes.Add("style", $"background-color: {(args.Data.Active ? "var(--rz-success)" : "var(--rz-danger)")};");
                }
                
            }

            
        }
    }
}
