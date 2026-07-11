using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Catalog
{
    public partial class Details : ComponentBase
    {
        private PageHeaderModel? _pageHeader;
        private ProductCatalogDetailDTO? _detail;
        private bool _loading = true;

        [Parameter]
        public int Id { get; set; }

        [Inject]
        private IProductCatalogService ProductCatalogService { get; set; } = default!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        private IHeaderService HeaderService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
        }

        protected override async Task OnParametersSetAsync()
        {
            _loading = true;
            _detail = await ProductCatalogService.GetDetailsAsync(Id);
            _loading = false;
            ApplyHeader();
        }

        private void ApplyHeader()
        {
            if (_pageHeader == null || _detail?.Product == null)
                return;

            _pageHeader.Title = _detail.Product.Name;
            _pageHeader.Subtitle = _detail.Product.ProductTypeName;

            // L'ultima voce del breadcrumb (l'id numerico) diventa il nome del prodotto.
            var last = _pageHeader.BreadcrumbItems?.LastOrDefault();
            if (last != null)
                last.Text = _detail.Product.Name;
        }

        private void BackToCatalog()
        {
            NavigationManager.NavigateTo("/Catalog");
        }
    }
}
