using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IProductCatalogService
    {
        Task<ProductCatalogPageDTO> GetPageAsync(ProductCatalogFilter? filter);

        Task<ProductCatalogDetailDTO?> GetDetailsAsync(int idProduct);
    }
}
