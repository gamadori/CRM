using CRM.Client.Helpers;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyProductCatalogService : IProductCatalogService
    {
        private readonly HttpClient _http;

        public ProxyProductCatalogService(HttpClient http)
        {
            _http = http;
        }

        public async Task<ProductCatalogPageDTO?> GetPageAsync(ProductCatalogFilter filter)
        {
            try
            {
                var url = $"{ConstHelper.ProductCatalogPath}{BuildQueryString(filter)}";
                return await _http.GetFromJsonAsync<ProductCatalogPageDTO>(url);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<ProductCatalogDetailDTO?> GetDetailsAsync(int idProduct)
        {
            try
            {
                return await _http.GetFromJsonAsync<ProductCatalogDetailDTO>($"{ConstHelper.ProductCatalogPath}/{idProduct}");
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
                return null;
            }
            catch
            {
                return null;
            }
        }

        public string GetAssetFileUrl(int idAsset)
        {
            return $"{ConstHelper.ProductCatalogAssetsPath}/{idAsset}/file";
        }

        private static string BuildQueryString(ProductCatalogFilter filter)
        {
            var values = new List<string>
            {
                $"PageNumber={filter.PageNumber}",
                $"PageSize={filter.PageSize}"
            };

            if (filter.IdProductType != null)
            {
                values.Add($"IdProductType={filter.IdProductType.Value}");
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                values.Add($"Search={Uri.EscapeDataString(filter.Search)}");
            }

            return values.Count == 0 ? string.Empty : $"?{string.Join("&", values)}";
        }
    }
}
