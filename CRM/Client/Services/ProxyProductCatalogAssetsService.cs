using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyProductCatalogAssetsService : ProxyRestClientService<ProductCatalogAsset, ProductCatalogAssetDTO, int, ProductCatalogAssetFilter, object>, IProductCatalogAssetsService
    {
        public ProxyProductCatalogAssetsService(HttpClient http) : base(http, ConstHelper.ProductCatalogAssetsPath)
        {
        }

        public async Task<APIResponseMessage<List<ProductCatalogAssetDTO>>> UploadAsync(ProductCatalogAssetUploadRequest request)
        {
            try
            {
                var response = await _http.PostAsJsonAsync($"{_pathService}/upload", request);
                var content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<APIResponseMessage<List<ProductCatalogAssetDTO>>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new APIResponseMessage<List<ProductCatalogAssetDTO>> { State = false, Message = "Empty server response" };
                }

                return new APIResponseMessage<List<ProductCatalogAssetDTO>>
                {
                    State = false,
                    Code = response.StatusCode,
                    Message = content
                };
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return new APIResponseMessage<List<ProductCatalogAssetDTO>> { State = false, Code = System.Net.HttpStatusCode.Unauthorized };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<List<ProductCatalogAssetDTO>> { State = false, Message = ex.Message };
            }
        }

        public async Task<bool> SetCoverAsync(int id)
        {
            try
            {
                var response = await _http.PostAsync($"{_pathService}/{id}/cover", null);
                return response.IsSuccessStatusCode;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return false;
            }
        }

        public async Task<byte[]> DownloadFileAsync(int id)
        {
            try
            {
                return await _http.GetByteArrayAsync($"{_pathService}/{id}/file");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return Array.Empty<byte>();
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }
}
