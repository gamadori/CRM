using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class ArticlesIndex: ComponentBase
    {
       
        [Inject]
        HttpClient HttpClient { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Parameter]
        public List<TicketInterventionArticleModel> InterventionArticles { get; set; }

        [Parameter]
        public int IdIntervention { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionArticleModel> OnDelete { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionArticleModel> OnUpdate { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public EventCallback<TicketInterventionArticleModel> OnAdd { get; set; }

        [Parameter]
        public EventCallback<List<TicketInterventionArticleModel>> InterventionArticlesChanged { get; set; }

       


        private List<Product> _products;

        private List<Article> _articles;

        private RadzenDataGrid<TicketInterventionArticleModel> _articlesGrid;

        private TicketInterventionArticleModel _interventionArticle;

        private bool _isLoading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await base.OnInitializedAsync();
            _isLoading = false;
        }

        private async Task LoadData()
        {

            var resp = await RestClientService.GetListPag<ProductFilter, Product>(new ProductFilter(), ConstHelper.Products);  // _productService.GetList(new ProductFilter()); //HttpClient.GetFromJsonAsync<List<Product>>(ConstHelper.Products);

            if (resp != null)
                _products = resp.Items;
            else
                _products = new List<Product>();

            await LoadArticles(null);
             
        }


        private async Task LoadArticles(int? idProduct)
        {


            
            ArticleFilter _filter = new ArticleFilter() { IdCompany = IdCompany, Skip = null, Top = null };

            if (idProduct != null && idProduct != 0)
                _filter.IdProduct = idProduct;

            var resp = await RestClientHelper.Get<Article>(HttpClient, ConstHelper.ArticlesPath, _filter);

            if (resp != null)
                _articles = resp.Items;
            else
                _articles = new List<Article>();

           
            StateHasChanged();

        }

        private async Task OnChangeProduct(TicketInterventionArticleModel? item)
        {
            await LoadArticles(item?.IdProduct);

            if (!_articles.Any(x => x.Id == item?.IdArticle))
                item.IdArticle = null;

        }
        async Task EditRow(TicketInterventionArticleModel item)
        {
            await _articlesGrid.EditRow(item);
            //await LoadArticles(item.IdProduct);
        }

        void OnUpdateRow(TicketInterventionArticleModel item)
        {
            if (item == _interventionArticle)
            {
                _interventionArticle = null;
            }

            
        }

        async Task SaveRow(TicketInterventionArticleModel item)
        { 
            if (item == _interventionArticle)
            {
                _interventionArticle = null;
            }
            if (item.IdProduct != null)
                item.Product = (await RestClientService.GetItem<Product, int>(item.IdProduct.Value, ConstHelper.Products)).Name; //(await _productService.Get(item.IdProduct.Value))?.Name;
            else
                item.Product = "";

            if (item.IdArticle != null)
                item.Article = (await RestClientService.GetItem<Article, int>(item.IdArticle.Value, ConstHelper.ArticlesPath))?.SerialNumber;
            else
                item.Article = "";

            await _articlesGrid.UpdateRow(item);
        }

        void CancelEdit(TicketInterventionArticleModel item)
        {
            if (item == _interventionArticle)
            {
                _interventionArticle = null;
            }

            _articlesGrid.CancelEditRow(item);

            
        }

        async Task DeleteRow(TicketInterventionArticleModel item)
        {
            if (item == _interventionArticle)
            {
                _interventionArticle = null;
            }

            if (await DialogService.Confirm(Localize["Eliminare il Dispositivo?"]) == true)
            {
                if (InterventionArticles.Contains(item))
                {

                    // For demo purposes only
                    InterventionArticles.Remove(item);

                    // For production
                    //dbContext.SaveChanges();

                    await _articlesGrid.Reload();

                    if (OnDelete.HasDelegate)
                        await OnDelete.InvokeAsync();

                }
                else
                {
                    _articlesGrid.CancelEditRow(item);
                }
            }
        }
        void OnCreateRow(TicketInterventionArticleModel item)
        {
            InterventionArticles.Add(item);
        }

        async Task InsertRow()
        {
            _interventionArticle = new TicketInterventionArticleModel() { Id = Guid.NewGuid()};
            await _articlesGrid.InsertRow(_interventionArticle);
        }

        
    }
}
