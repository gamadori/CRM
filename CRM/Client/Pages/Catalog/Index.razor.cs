using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Catalog
{
    public partial class Index : ComponentBase
    {
        private const int PageSize = 24;

        private PageHeaderModel? _pageHeader;
        private ProductCatalogPageDTO? _page;
        private bool _loading = true;
        private int? _selectedTypeId;
        private int _pageNumber = 1;
        private string _searchText = string.Empty;
        private string _appliedSearch = string.Empty;

        [Inject]
        private IProductCatalogService ProductCatalogService { get; set; } = default!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        private IHeaderService HeaderService { get; set; } = default!;

        private bool CanGoPrevious => _page != null && _page.PageNumber > 1;

        private bool CanGoNext => _page != null && _page.TotalPages > _page.PageNumber;

        private IEnumerable<CatalogGroup> CurrentPageGroups => (_page?.Products ?? new List<ProductCatalogListItemDTO>())
            .GroupBy(x => new
            {
                x.IdProductType,
                Label = string.IsNullOrWhiteSpace(x.ProductTypeName) ? "Senza tipo" : x.ProductTypeName
            })
            .OrderBy(x => x.Key.Label)
            .Select(x => new CatalogGroup(x.Key.IdProductType, x.Key.Label, x.ToList()));

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadPage();
        }

        private async Task LoadPage()
        {
            _loading = true;

            _page = await ProductCatalogService.GetPageAsync(new ProductCatalogFilter
            {
                PageNumber = _pageNumber,
                PageSize = PageSize,
                IdProductType = _selectedTypeId,
                Search = _appliedSearch
            });

            _loading = false;
        }

        private async Task SelectType(int? idProductType)
        {
            _selectedTypeId = idProductType;
            _pageNumber = 1;
            await LoadPage();
        }

        private async Task ApplySearch()
        {
            _appliedSearch = _searchText.Trim();
            _pageNumber = 1;
            await LoadPage();
        }

        private async Task SearchKeyDown(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                await ApplySearch();
            }
        }

        private async Task PreviousPage()
        {
            if (!CanGoPrevious)
            {
                return;
            }

            _pageNumber--;
            await LoadPage();
        }

        private async Task NextPage()
        {
            if (!CanGoNext)
            {
                return;
            }

            _pageNumber++;
            await LoadPage();
        }

        private void OpenProduct(int idProduct)
        {
            NavigationManager.NavigateTo($"/Catalog/Details/{idProduct}");
        }

        private string TypeButtonClass(int? idProductType)
        {
            return _selectedTypeId == idProductType ? "active" : string.Empty;
        }

        private record CatalogGroup(int? Key, string Label, List<ProductCatalogListItemDTO> Products);
    }
}
