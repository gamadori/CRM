using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
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

namespace CRM.Client.Pages.Products
{
    [Authorize(Roles = "SuperUser,Admin")]
    public partial class Edit : ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }
        
        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int? IdParent { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private Product _product = null;

        private List<ProductType> _productTypes;

        private string _messageState = "";

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {

                await GetProductTypes();

                if (Id != null)
                {

                    
                    _product = await RestClientService.GetItem<Product, int>(Id.Value, ConstHelper.Products); // await Service.Get(Id.Value);
                }
                else
                {
                    
                    _product = new Product();
                }

                //_pageHeader = HeaderService.Create("Products", Id, _product?.Name, true, ConstHelper.ClientProductsPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {
                var resp = await RestClientService.Post<Product, int>(_product, ConstHelper.Products);

                if (resp.State) // ==  await Service.Post(_product) !=null)
                {
                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo("/Products");
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
                NavigationManager.NavigateTo("/Products/Index");
        }

        private async Task GetProductTypes()
        {
            //_productTypes = await ServiceProductType.Get();
            _productTypes = await RestClientService.Get<ProductType>(ConstHelper.ProductTypesPath);

        }
        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }

    }
}
