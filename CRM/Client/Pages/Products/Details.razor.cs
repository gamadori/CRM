using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Products
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        //[Inject]
        //private IBaseRestService<Product, ProductFilter, int> _service { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action<int>OnClickEditChild { get; set; } 

        [Parameter]
        public Action OnClickCancel { get; set; }

        private Product _product = null;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.GroupsPath;

                _product = await RestClientService.GetItem<Product, int>(Id, ConstHelper.Products);   // _service.Get(Id);

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

     

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Products/Edit/{Id}");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/Products/Index");
        }


    }
}
