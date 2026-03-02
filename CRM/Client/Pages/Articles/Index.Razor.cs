using BlazoringComponents;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using QLNet;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Articles
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        NavigationManager NavigationManager { get; set; }

       

        [Inject]       
        IArticlesService Service { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }

        [Inject]
        IProductsService ProductsService { get; set; }

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
        public int? IdCompany { get; set; }

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

       
        private IQueryable<ArticleDTO> _articles = null;


        private List<CompanyDTO> _companies;

        private List<ProductDTO> _products;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ArticleFilter _filter = new ArticleFilter() { PageSize = ConstHelper.PageSize, Skip = 0, Top = ConstHelper.PageSize };

        private string _messageDelete = "";

        private string _header;

        private Article _article ;

        private int _companiesCount = 0;

        private int _productsCount = 0;

        private string pagingSummaryFormat;

        private bool _isLoading = false;

        private const int _companyPageSize = 10;

        private const int _articletPageSize = 10;

        private const int _productPageSize = 10;

        private RadzenDataGrid<ArticleDTO> grdArticles;

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

            await LoadCompanies();
            await LoadProduct();

            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

            await LoadData();

            //_pageHeader = HeaderService.Create("Articles", null, null, false, ConstHelper.ClientArticlesPath, null, PageMode);
            _pageHeader = await HeaderService.Create(PageMode);
        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<Article>().AsQueryable();
            try
            {
                _header = Localize["Articles"];

                if (IdCompany != null)
                    _filter.IdCompany = IdCompany;

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await Service.GetPagingAsync(_filter);

                _articles = pagingResponse.Items.AsQueryable();
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

        public async Task LoadProduct()
        {
            _products = await ProductsService.GetListAsync(null);

            await InvokeAsync(StateHasChanged);
        }
        public async Task LoadCompanies()
        {
            _companies = await CompaniesService.GetListAsync(null);

            await InvokeAsync(StateHasChanged);
        }

       

        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/{id}");
        }

        

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}");
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/New");
        }

        protected async Task Delete(ArticleDTO article)
        {

            if (await DialogService.Confirm(string.Format(Localize["Elliminare l'articolo {0}?"], article.Name)) == true)
            {
                
                if (OnClickDelete != null)
                    OnClickDelete(article.Id);
                else
                {
                    await Service.DeleteAsync(article.Id);


                    await LoadData();
                }
                
            }
        }

        
        #region Filter
        protected async void OnChangeSerialNumberFilter(ChangeEventArgs args)
        {
            
            _filter.SerialNumber = args.Value.ToString();
            await grdArticles.GoToPage(0);
            await LoadData();
        }

        protected async void OnChangeCompany(int? id)
        {
            _filter.IdCompany = id; 
            await grdArticles.GoToPage(0);
            await LoadData();
            
        }

        protected async void OnChangeProduct(int? id)
        {
            _filter.IdProduct = id;
            await grdArticles.GoToPage(0);
            await LoadData();
            
        }

        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.IdCompany = null;
                _filter.IdProduct = null;
                _filter.SerialNumber = "";


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
