using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IProductCatalogService
    {
        Task<ProductCatalogPageDTO?> GetPageAsync(ProductCatalogFilter filter);

        Task<ProductCatalogDetailDTO?> GetDetailsAsync(int idProduct);

        string GetAssetFileUrl(int idAsset);
    }
}
