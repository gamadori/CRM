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

namespace CRM.Client.Pages.ProductAccTypes
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

       
        [Inject]
        private IProductAccTypesService ProductTypeAccTypesService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IRestService<ApplicationUser> UserSignedService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }

        [Parameter]
        public int IdProductType { get; set; }

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

        private PagingResponse<ProductAccessoryTypeModel> _prodTypeAccs = null;

        private bool _isLoading = false;

        private RadzenDataGrid<ProductAccessoryTypeModel> _grdProductTypeAccType;

        private string _header = "Product Types Accessories";

        private bool _filterState = false;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>() ;

        private ApplicationUser? _user;

        private FilterMode _filterMode = FilterMode.Advanced;

        protected async override Task OnInitializedAsync()
        {
            _bread = await BreadCrumbService.ProductTypeAccs(false);

            _header = Localize["Product Accessory"];

            _user = await UserSignedService.Get();

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            await LoadData();
            
            StateHasChanged();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            ProductAccessoryTypeFilter paging = new ProductAccessoryTypeFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {
                paging.IdProduct = IdProductType;
                
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

                _prodTypeAccs = await ProductTypeAccTypesService.Get(paging);
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_prodTypeAccs == null)
                {
                    _prodTypeAccs = new PagingResponse<ProductAccessoryTypeModel>();

                    _prodTypeAccs.Items = new List<ProductAccessoryTypeModel>();
                    _prodTypeAccs.MetaData = new PagingHeaderModel();

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
                
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccTypesPath}/Details/{id}");
            }
        }

        private void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccTypesPath}/Edit/{id}");
        }

        protected async Task Delete(int id)
        {

            if (await DialogService.Confirm(Localize["Eliminare l'accessorio selezionato"], Localize["Elimina"]) == true)
            {
                if (OnClickDelete != null)
                    OnClickDelete(id);
                else
                {
                    await ProductTypeAccTypesService.Delete(id);

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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccTypesPath}/Edit");
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

        private void LanguageRow(int id)
        {

            NavigationManager.NavigateTo($"{ConstHelper.ClientProductAccLangsPath}/Index/{id}");


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
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientProductAccLangsPath}/{id}");
                    break;
            }

        }
    }
}
