using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IProductCatalogAssetsService
    {
        Task<ProductCatalogAssetDTO?> GetItemAsync(int id);

        Task<PagingResponse<ProductCatalogAssetDTO>?> GetPagingAsync(ProductCatalogAssetFilter? args = null);

        Task<List<ProductCatalogAssetDTO>?> GetListAsync(ProductCatalogAssetFilter? args = null);

        Task<APIResponseMessage<ProductCatalogAssetDTO>> PostAsync(ProductCatalogAsset item);

        Task<APIResponseMessage<List<ProductCatalogAssetDTO>>> UploadAsync(ProductCatalogAssetUploadRequest request);

        Task<bool> DeleteAsync(int id);

        Task<bool> SetCoverAsync(int id);

        Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFileAsync(int id);
    }
}
