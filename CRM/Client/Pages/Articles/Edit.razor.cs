using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
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

namespace CRM.Client.Pages.Articles
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        ICompaniesService CompaniesService { get; set; }

       
        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }   


        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Article _article = null;

        private List<Company> _companies = new List<Company>();

        private List<Product> _products = new List<Product>();

        private string _messageState = "";

        private string _header = "Article";

        private bool _lockCompany = false;

        private int _pageSize = 12;

        private int _companiesCount;

        private int _productsCount;

       

        protected override async Task OnInitializedAsync()
        {
            try
            {
                await LoadCompany();

                await LoadProducts();

                if (Id != null)
                {

                    _header = "Edit Article";
                    _article = await RestClientService.GetItem<Article, int>(Id.Value, ConstHelper.ArticlesPath);
                }
                else
                {
                    _header = "New Article";
                    _article = new Article();

                    if (IdCompany != null)
                    {
                        _article.IdCompany = IdCompany;
                        _lockCompany = true;
                    }
                }

               

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        public async Task LoadCompany()
        {
            //CompaniesFilterModel request = new CompaniesFilterModel() { PageSize = 0 };

            //request.PageSize = 0;

            //if (args != null)
            //{
            //    request.RagioneSociale = args.Filter;
            //    request.Skip = args.Skip;
            //    request.Top = args.Top;
            //}

            var response = await CompaniesService.Get<Company>(ConstHelper.CompaniesPath);
            _companiesCount = response.Count();

            _companies = response;
            await InvokeAsync(StateHasChanged);

        }

        public async Task LoadProducts()
        {

            var response = await RestClientService.Get<Product>(ConstHelper.Products); //await RestClientService.GetListPag<ProductFilter, Product>(new ProductFilter(), ConstHelper.Products);
            _products = response;
            _productsCount = response.Count();
         
        }

        protected async Task HandleValidSubmitAsync()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Article, int>(_article, ConstHelper.ArticlesPath);
                if (resp != null && resp.State)
                {
                    _article = resp.Data;
                    
                    if (PageMode == PageModality.Dialog)
                    {
                        DialogService.CloseSide(_article.Id);
                    }
                    else if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}");
                }
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientArticlesPath}/Index");
        }

        private async Task OnGetCompany(int? id)
        {
            if (id != null)
            {
                await LoadCompany();
              
                StateHasChanged();
                _article.IdCompany = (int)id;
                StateHasChanged();

            }
        }

    }
}
