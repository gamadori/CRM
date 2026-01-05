using CRM.Client.Helpers;
using CRM.Client.Models;
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

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }


        private ProductType _productType = null;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _productType = await RestClientService.GetItem<ProductType, int>(Id, ConstHelper.ProductTypesPath);   // Service.Get(Id);
                _pageHeader = await HeaderService.Create();
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
                NavigationManager.NavigateTo($"/Settings/ProductsTypes/{Id}/Edit");
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
