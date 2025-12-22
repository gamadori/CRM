using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Products
{
    public partial class AddChild : ComponentBase
    {
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        private IManyToManyService<ProductParentChildModel> _parentChildService { get; set; }

        [Inject] 
        private DialogService dialogService { get; set; }

        [Parameter]
        public int IdParent { get; set; }

        [Parameter]
        public Action OnClickClose { get; set; }

        private List<ProductFiltered> _products { get; set; }
        

        private ProductParentChildModel _parentChild = null;

        protected override async Task OnInitializedAsync()
        {
           
            await LoadsChilds(new LoadDataArgs());
            _parentChild = new ProductParentChildModel() { IdParent = IdParent };
          

        }
        public async Task LoadsChilds(LoadDataArgs args)
        {
            ProductFilter request = new ProductFilter();

            if (args != null && !string.IsNullOrEmpty(args.Filter))
            {
                request.Name = args.Filter;
            }
            var response = await RestClientService.Get<Product, ProductFilter>(request, ConstHelper.Products);  // _service.Get(request);

            _products = response.Items.Select(x=>new ProductFiltered() { Id = x.Id, Name = x.Name  }).ToList();

            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
           

            await _parentChildService.Post(_parentChild);
            dialogService.Close();
            
        }

        protected void Cancel()
        {
            dialogService.Close();
        }
    }

    public class ProductFiltered
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    
}
