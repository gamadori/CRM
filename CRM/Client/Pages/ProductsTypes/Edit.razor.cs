using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.ProductsTypes
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        
        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private ProductType _productType = null;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        protected override async Task OnInitializedAsync()
        {
            

            string path;
            try
            {
                _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "/Settings" });
                _bread.Add(new BreadcrumbModel() { Title = Localize["Tipo Prodotto"], Url = "/Settings/ProductsTypes" });
                
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.CompaniesPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                    _productType = await RestClientService.GetItem<ProductType, int>(Id.Value, ConstHelper.ProductTypesPath);   // await Service.Get(Id.Value);
                    _bread.Add(new BreadcrumbModel() { Title = _productType?.Name, Url = $"/Settings/TicketsTypes/{Id}" });
                    _bread.Add(new BreadcrumbModel() { Title = Localize["Modifica"], Url = null });
                }
                else
                {
                    _bread.Add(new BreadcrumbModel() { Title = Localize["Nuovo"], Url = null });
                    _productType = new ProductType();
                }
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {

            try
            {
                var resp = await RestClientService.Post<ProductType, int>(_productType, ConstHelper.ProductTypesPath);  // await Service.Post(_productType);

                

                if (OnClickSave != null)
                    OnClickSave();
                else
                    NavigationManager.NavigateTo("/Settings/ProductsTypes/Index");
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
                NavigationManager.NavigateTo("/Settings/ProductsTypes/Index");
        }

        

        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }


    }
}
