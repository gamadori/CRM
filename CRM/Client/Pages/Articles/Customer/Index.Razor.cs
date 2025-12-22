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

namespace CRM.Client.Pages.Articles.Customer
{
    [Authorize]
    public partial class Index: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }


        private PagingHeaderModel _paging = new PagingHeaderModel();

        private ArticleFilter _filter = new ArticleFilter();


        private bool _isLoading = false;


        private RadzenDataGrid<Article> grdArticles;

        private PagingResponse<Article> _articles = null;

        private const int _articletPageSize = 10;

        protected override async Task OnInitializedAsync()
        {
     


            await LoadData();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            ArticleFilter paging = new ArticleFilter() { PageSize = 10, Skip = 0, Top = 10 };

            _isLoading = true;

            try
            {

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

                
                _articles = await RestClientHelper.Get<Article>(HttpClient, ConstHelper.ArticlesPath, paging);

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_articles == null)
                {
                    _articles = new PagingResponse<Article>();
                    _articles.Items = new List<Article>();
                    _articles.MetaData = new PagingHeaderModel();
                }
                _isLoading = false;
                StateHasChanged();
            }

        }

       

        protected void Details(int id)
        {
            NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/{id}");
        }

        

    }
}
