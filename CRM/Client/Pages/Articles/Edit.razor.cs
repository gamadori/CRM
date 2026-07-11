using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Shared;
using CRM.Shared.DTOs;
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
        IProductsService ProductsService { get; set; }

        [Inject]
        IArticlesService Service { get; set; }


        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public int? IdCompany { get; set; } = null;
        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Article _article = null;

        private List<CompanyDTO> _companies = new List<CompanyDTO>();

        private List<ProductDTO> _products = new List<ProductDTO>();

        private string _messageState = "";

        private string _header = "Article";

        private bool _lockCompany = false;

        

        private string _subTitle = "";

       private PageHeaderModel? _pageHeader = null;


        protected override async Task OnInitializedAsync()
        {
            try
            {
                await LoadCompanies();

                await LoadProducts();

                if (Id != null)
                { 
                    var dto = await Service.GetItemAsync(Id.Value);
                    _article = dto.ToEntity();
                }
                else
                {
                    _subTitle = Localize["Nuovo Articolo"];
                    _article = new Article();

                    if (IdCompany != null)
                    {
                        _article.IdCompany = IdCompany;
                        _lockCompany = true;
                    }
                }

                //_pageHeader = HeaderService.Create("Articles", Id, _article?.SerialNumber, true, ConstHelper.ClientArticlesPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        public async Task LoadCompanies()
        {
            var response = await CompaniesService.GetListAsync(null);
           
            _companies = response;
            await InvokeAsync(StateHasChanged);

        }

        public async Task LoadProducts()
        {

            _products = await ProductsService.GetListAsync(null);
            await InvokeAsync(StateHasChanged);
        }

        protected async Task HandleValidSubmitAsync()
        {
            _messageState = "";
            try
            {
                var resp = await Service.PostAsync(_article);
                if (resp != null && resp.State)
                {
                    _article = resp.Data?.ToEntity();
                    
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

        private async Task OnGetCompany(object? id)
        {
            if (id != null)
            {
                await LoadCompanies();

                StateHasChanged();
                _article.IdCompany = (int)id;
                StateHasChanged();

            }
        }

        private async Task OnGetRecipientCompany(object? id)
        {
            if (id != null)
            {
                await LoadCompanies();

                StateHasChanged();
                _article.IdRecipientCompany = (int)id;
                StateHasChanged();
            }
        }

    }
}
