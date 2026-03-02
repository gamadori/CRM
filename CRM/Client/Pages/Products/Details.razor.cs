using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Products
{
    [Authorize]
    public partial class Details: ComponentBase
    {
       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        //[Inject]
        //private IBaseRestService<Product, ProductFilter, int> _service { get; set; }

        [Inject]
        IProductsService ProductsService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action<int>OnClickEditChild { get; set; } 

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private ProductDTO _product = null;

        private PageHeaderModel? _pageHeader = null;
        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.GroupsPath;

                _product = await ProductsService.GetItemAsync(Id);   // _service.Get(Id);
                
                //_pageHeader = HeaderService.Create("Products", Id, _product?.Name, true, ConstHelper.ClientProductsPath, null, PageMode);
                _pageHeader = await HeaderService.Create(PageMode);
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
                NavigationManager.NavigateTo($"/Products/{Id}/Edit");
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
