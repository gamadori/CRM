using BlazoringComponents;
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
using Syncfusion.Blazor.Grids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Products
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        //[Inject]
        //private IBaseRestService<Product, ProductFilter, int> _service { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? IdParent { get; set; } = null;

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }
        
        
        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public string MessagePrepareDelete { get; set; }

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private IQueryable<Product> _products = null;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ProductFilter _filter = new ProductFilter() { PageSize = ConstHelper.PageSize, Skip = 0, Top = ConstHelper.PageSize };

        private string _messageDelete = "";

        private string _header;

        private Product _product;

        private string pagingSummaryFormat;

        private bool _isLoading = false;

        private RadzenDataGrid<Product> grdProducts;

        private int _productPageSize = 10;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];
            navMenuService.CallRequestRefresh();
            await LoadData();

            //_pageHeader = HeaderService.Create("Products", null, null, false, ConstHelper.ClientProductsPath, null);
            _pageHeader = await HeaderService.Create(PageMode);



        }

        public async Task LoadData(LoadDataArgs args = null)
        {
            _isLoading = true;

            
            try
            {
                _filter.PageSize = _productPageSize;
               
                if (IdParent != null)
                {
                    _filter.IdParent = IdParent;
                    _header = "SOTTO PARTI";
                }
                else
                {
                    _header = "TIPI PRODOTTO";
                }

                
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }

                _filter.PageSize = _productPageSize;

                var pagingResponse = await RestClientService.Get<Product, ProductFilter>(_filter, ConstHelper.Products);    // _service.Get(_filter);

                _products = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);  
            }
            finally
            {
                if (_products == null)
                    _products = Enumerable.Empty<Product>().AsQueryable();
                _isLoading = false;

                await InvokeAsync(StateHasChanged);

            }
     
        }

        protected void Details(int idProduct)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idProduct);
            }
            else
                NavigationManager.NavigateTo($"/Products/{idProduct}");
        }


        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/Products/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo("/Products");
        }
        protected void NewItem()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/Products/New");
        }

        protected async Task Delete(Product item)
        {

         
            
            if (item != null)
            {
                if (OnClickDelete != null)
                    OnClickDelete(item.Id);
                else
                {
                    if (await DialogService.Confirm($"{Localize["Eliminare il prodotto"]} {item.Name}" ) == true)
                    {
                        await RestClientService.Delete<int>(item.Id, ConstHelper.Products); // _service.Delete(item.Id);


                        await LoadData();
                    }
                }
            }
        }
      
        protected async Task OnChangeNameFilter(ChangeEventArgs args)
        {
            _filter.Name = args.Value.ToString();
            await grdProducts.GoToPage(0);
            await LoadData();
        }


        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.Name = "";
            }
            await LoadData();
        }

        protected void ImportData()
        {
            NavigationManager.NavigateTo($"/CSVSettings/CSVData/{CSVTable.Category.ToString()}");
        }

       
    }
}
