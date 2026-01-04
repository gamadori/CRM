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

namespace CRM.Client.Pages.ArticleBackups
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


        [Parameter]
        public int? IdArticle { get; set; }

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

       
        private IQueryable<ArticleBackup> _articleBackups = null;


        private List<CompanyFilter> _companies;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ArticleBackupFilter _filter = new ArticleBackupFilter() { PageSize = ConstHelper.PageSize, Skip = 0, Top = ConstHelper.PageSize };

        private string _messageDelete = "";

        private string _header;

        private Article _article ;

        private int _companiesCount = 0;

        private int _productsCount = 0;

        private string pagingSummaryFormat;

        private bool _isLoading = false;

       
        private const int _itemsPageSize = 10;

        private RadzenDataGrid<ArticleBackup> grdArticles;

        private FilterMode _filterMode = FilterMode.Advanced;

        private bool _isMobile = false;

        private bool _isResponsable = false;

        private List<BreadcrumbModel> _breadCrumb;

        protected override async Task OnInitializedAsync()
        {

            _isLoading = true;

            // navMenuService.CallRequestRefresh();
            await FindResponsiveness();
            

            if (PageMode == PageModality.Dialog)
                _filterMode = FilterMode.SimpleWithMenu;

            pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<ArticleBackup>().AsQueryable();
            try
            {
                
                _header = Localize["Articles"];

                if (IdArticle != null)
                    _filter.IdArticle = IdArticle;

                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await RestClientService.Get<ArticleBackup, ArticleBackupFilter>(_filter, ConstHelper.ArticleBackupsPath); 

                _articleBackups = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

                _isLoading = false;
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticleBackupsPath}/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientArticleBackupsPath}");
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
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticleBackupsPath}/Edit");
        }

        protected async Task Delete(ArticleBackup item)
        {

            if (await DialogService.Confirm(string.Format(Localize["Elliminare il backup del {0}?"], item.TimeStamp)) == true)
            {
                
                if (OnClickDelete != null)
                    OnClickDelete(item.Id);
                else
                {
                    await RestClientService.Delete<int>(item.Id, ConstHelper.ArticleBackupsPath);


                    await LoadData();
                }
                
            }
        }

        
        #region Filter
        

        protected async void OnChangeCompany(object value, string name)
        {
            var str = value;
            await grdArticles.GoToPage(0);
            await LoadData();
            
        }

        protected async void OnChangeProduct(object value, string name)
        {
            await grdArticles.GoToPage(0);
            await LoadData();
            
        }

        protected async void OnChangeFilter(bool state)
        {

            if (!state)
            {
                _filter.IdArticle = IdArticle;
                _filter.TimeStampFrom = null;
                _filter.TimeStampTo = null;


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
