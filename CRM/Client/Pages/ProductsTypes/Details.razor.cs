using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ProductsTypes
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private ProductType _productType = null;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();
        protected override async Task OnInitializedAsync()
        {
            try
            {

                _productType = await RestClientService.GetItem<ProductType, int>(Id, ConstHelper.ProductTypesPath);   // Service.Get(Id);

                _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url = "/Settings" });
                _bread.Add(new BreadcrumbModel() { Title = Localize["Tipo Prodotto"], Url = "/Settings/ProductsTypes" });
                _bread.Add(new BreadcrumbModel() { Title = _productType.Name, Url = null });

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

       

        protected void EditProductType()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Settings/ProductsTypes/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Settings/ProductsTypes/Index");
        }

       
       

    }
}
