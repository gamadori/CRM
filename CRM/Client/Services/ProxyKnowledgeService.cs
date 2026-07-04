using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyKnowledgeService : IKnowledgeApiService
    {
        private const string Path = "api/ProductKnowledge";
        private readonly HttpClient _http;

        public ProxyKnowledgeService(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ProductKnowledgeDTO>> GetList(ProductKnowledgeFilter? filter = null)
        {
            var query = new List<string>();
            if (filter != null)
            {
                if (filter.IdProduct.HasValue)
                    query.Add($"IdProduct={filter.IdProduct.Value}");
                if (!string.IsNullOrWhiteSpace(filter.Search))
                    query.Add($"Search={Uri.EscapeDataString(filter.Search)}");
                if (!string.IsNullOrWhiteSpace(filter.Category))
                    query.Add($"Category={Uri.EscapeDataString(filter.Category)}");
            }

            var url = query.Count > 0 ? $"{Path}?{string.Join("&", query)}" : Path;

            try
            {
                return await _http.GetFromJsonAsync<List<ProductKnowledgeDTO>>(url) ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetList KB: {ex.Message}");
                return new();
            }
        }

        public async Task<ProductKnowledgeDTO?> Get(int id)
        {
            try
            {
                return await _http.GetFromJsonAsync<ProductKnowledgeDTO>($"{Path}/{id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore Get KB: {ex.Message}");
                return null;
            }
        }

        public async Task<ProductKnowledgeDTO?> Save(ProductKnowledge item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync(Path, item);
                return resp.IsSuccessStatusCode
                    ? await resp.Content.ReadFromJsonAsync<ProductKnowledgeDTO>()
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore Save KB: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> Delete(int id)
        {
            try
            {
                var resp = await _http.DeleteAsync($"{Path}/{id}");
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore Delete KB: {ex.Message}");
                return false;
            }
        }

        public async Task<KnowledgeEmbeddingStats?> GenerateEmbeddings(int batchSize = 20)
        {
            try
            {
                var resp = await _http.PostAsync($"{Path}/generate-embeddings?batchSize={batchSize}", null);
                return resp.IsSuccessStatusCode
                    ? await resp.Content.ReadFromJsonAsync<KnowledgeEmbeddingStats>()
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GenerateEmbeddings KB: {ex.Message}");
                return null;
            }
        }

        public async Task<KnowledgeImportResult?> ImportDocument(Stream fileStream, string fileName, int? idProduct, string? category)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StreamContent(fileStream), "file", fileName);

                if (idProduct.HasValue)
                    content.Add(new StringContent(idProduct.Value.ToString()), "idProduct");
                if (!string.IsNullOrWhiteSpace(category))
                    content.Add(new StringContent(category), "category");

                var resp = await _http.PostAsync($"{Path}/import-document", content);
                return resp.IsSuccessStatusCode
                    ? await resp.Content.ReadFromJsonAsync<KnowledgeImportResult>()
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore ImportDocument KB: {ex.Message}");
                return null;
            }
        }
    }
}
