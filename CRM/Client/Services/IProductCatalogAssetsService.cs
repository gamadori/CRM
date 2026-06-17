using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IProductCatalogAssetsService : IDataService<ProductCatalogAsset, ProductCatalogAssetDTO, int, ProductCatalogAssetFilter, object>
    {
        Task<APIResponseMessage<List<ProductCatalogAssetDTO>>> UploadAsync(ProductCatalogAssetUploadRequest request);

        Task<bool> SetCoverAsync(int id);

        Task<byte[]> DownloadFileAsync(int id);

        Task<ProductCatalogFileResult?> GetFileAsync(int id);
    }

    public sealed class ProductCatalogFileResult
    {
        public byte[] Content { get; init; } = System.Array.Empty<byte>();

        public string ContentType { get; init; } = "application/octet-stream";
    }
}
