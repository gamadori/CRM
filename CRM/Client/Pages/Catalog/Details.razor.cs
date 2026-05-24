using CRM.Client.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Catalog
{
    public partial class Details : ComponentBase
    {
        private ProductCatalogDetailDTO? _detail;
        private bool _loading = true;

        [Parameter]
        public int Id { get; set; }

        [Inject]
        private IProductCatalogService ProductCatalogService { get; set; } = default!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        protected override async Task OnParametersSetAsync()
        {
            _loading = true;
            _detail = await ProductCatalogService.GetDetailsAsync(Id);
            _loading = false;
        }

        private void BackToCatalog()
        {
            NavigationManager.NavigateTo("/Catalog");
        }
    }
}
