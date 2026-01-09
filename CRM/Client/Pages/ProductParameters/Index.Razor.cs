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
using Radzen;
using Radzen.Blazor;
using Microsoft.Extensions.Localization;
using CRM.Shared.Helper;
using static CRM.Client.Helpers.PageHelper;
using CRM.Client.Models;

namespace CRM.Client.Pages.ProductParameters
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        NavigationManager NavigationManager { get; set; }

       

        [Inject]       
        IAGRestClientService RestClientService { get; set; }

        [Inject] 
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IJSRuntime JsRuntime { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? IdProduct { get; set; }

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

        [Parameter]
        public EventCallback<int?> OnSelectArticle { get; set; }

        [Parameter]
        public EventCallback OnNewArticle { get; set; }

       
        private IQueryable<ProductParameter> _parameters = null;

        private List<ProductFilter> _products;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ProductParameterFilter _filter = new ProductParameterFilter() { PageSize = ConstHelper.PageSize, Skip = 0, Top = ConstHelper.PageSize };

        private string _messageDelete = "";

        private string _header;

        private Article _article ;

        private int _productsCount = 0;

        private int _itemsCount = 0;

        private string pagingSummaryFormat;

        private bool _isLoading = false;

      

        private RadzenDataGrid<ProductParameter> grdItems;

        private FilterMode _filterMode = FilterMode.Advanced;

        private bool _isMobile = false;

        private bool _isResponsable = false;

       
        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {


            // navMenuService.CallRequestRefresh();
            await FindResponsiveness();
            _isLoading = true;

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

             pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

            //_pageHeader = HeaderService.Create(ConstHelper.ClientProductParametersPath);

            _pageHeader = await HeaderService.Create(PageMode);

        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<ProductParameter>().AsQueryable();
            try
            {
                
                _header = Localize["Product Parameters"];

                if (IdProduct != null)
                    _filter.IdProduct = IdProduct;


                

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await RestClientService.Get<ProductParameter, ProductParameterFilter>(_filter, ConstHelper.ProductParameters); 

                
                _paging = pagingResponse.MetaData;

                
               
                
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                
            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
     
        }

        public async Task LoadProduct(LoadDataArgs args)
        {
            ProductFilter request = new ProductFilter();
            request.PageSize = 0;
            if (args != null)
            {
                request.Name = args.Filter;
                request.Skip = args.Skip;
                request.Top = args.Top;
               
            }
            var response = await RestClientService.Get<Product, ProductFilter>(request, ConstHelper.Products); // await _serviceProduct.Get(request);

            _products = response.Items.Select(x => new ProductFilter() { Id = x.Id, Name = x.Name  }).ToList();
            _productsCount = response.MetaData.TotalCount;
            await InvokeAsync(StateHasChanged);
        }
      

        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductParametersPath}/{id}");
        }

        

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductParametersPath}/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientProductParametersPath}");
        }
        protected async void NewItem()
        {

            if (OnNewArticle.HasDelegate)
            {
                await OnNewArticle.InvokeAsync();
            }
            else if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientProductParametersPath}/New");
        }

        protected async Task Delete(ProductParameter item)
        {

            if (await DialogService.Confirm(string.Format(Localize["Elliminare il parametro {0}?"], item.Name)) == true)
            {
                
                if (OnClickDelete != null)
                    OnClickDelete(item.Id);
                else
                {
                    await RestClientService.Delete<int>(item.Id, ConstHelper.ProductParameters);


                    await LoadData();
                }
                
            }
        }

        
        #region Filter
        protected async void OnChangeNameFilter(ChangeEventArgs args)
        {
            
            _filter.Name = args.Value.ToString();
            await grdItems.GoToPage(0);
            await LoadData();
        }

       

        protected async void OnChangeProduct(object value, string name)
        {
            await grdItems.GoToPage(0);
            await LoadData();
            
        }

        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
               
                _filter.IdProduct = null;
               

            }
            await LoadData();
        }

        #endregion
        protected async Task PageChanged(Radzen.PagerEventArgs args)
        {
            _filter.PageNumber = args.PageIndex + 1;
            await LoadData();
            StateHasChanged();

        }

        protected void ImportData()
        {
            NavigationManager.NavigateTo($"/CSVSettings/CSVData/{CSVTable.Article.ToString()}");
        }

        private async Task OnClickSerialNumber(int? id)
        {
            switch (PageMode)
            {
                case PageModality.Dialog:

                    if (OnSelectArticle.HasDelegate)
                    {
                        await OnSelectArticle.InvokeAsync(id);
                    }
                    else
                        DialogService.CloseSide(id);
                    break;
                case PageModality.Visualization:
                    //NavigationManager.NavigateTo($"/Articles/{id}");
                    Details((int)id);
                    break;
            }

        }

        public async Task FindResponsiveness()
        {
            _isMobile = await JsRuntime.InvokeAsync<bool>("isDevice");
        }

        private bool ColVisible()
        {
            return !_isMobile || _isResponsable;
        }

    }
}
