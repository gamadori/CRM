using CRM.Client.Services;   // ILogEventService
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Implementazione della base di conoscenza. Gli embedding sono calcolati con
    /// <see cref="OpenAIEmbeddingService"/> (stesso meccanismo dei ticket).
    /// </summary>
    public class KnowledgeService : IKnowledgeService
    {
        // Bonus di similarità (punti percentuali) per le voci del modello coinvolto
        private const double ProductMatchBonus = 15.0;

        // Parametri di suddivisione dei documenti importati
        private const int MaxChunkLength = 1800;
        private const int MaxChunksPerImport = 300;

        private readonly ApplicationDbContext _context;
        private readonly OpenAIEmbeddingService _embeddingService;
        private readonly ILogEventService _logEventService;

        public KnowledgeService(
            ApplicationDbContext context,
            OpenAIEmbeddingService embeddingService,
            ILogEventService logEventService)
        {
            _context = context;
            _embeddingService = embeddingService;
            _logEventService = logEventService;
        }

        public async Task<List<ProductKnowledgeDTO>> GetListAsync(ProductKnowledgeFilter? filter = null)
        {
            var q = _context.ProductKnowledge.AsNoTracking().AsQueryable();

            if (filter != null)
            {
                if (filter.IdProduct.HasValue)
                    q = q.Where(k => k.IdProduct == filter.IdProduct.Value);

                if (!string.IsNullOrWhiteSpace(filter.Category))
                    q = q.Where(k => k.Category == filter.Category);

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var s = filter.Search;
                    q = q.Where(k => k.Title.Contains(s) || k.Content.Contains(s));
                }
            }

            return await q
                .OrderByDescending(k => k.UpdatedAt ?? k.CreatedAt)
                .Take(500)
                .Select(k => new ProductKnowledgeDTO
                {
                    Id = k.Id,
                    IdProduct = k.IdProduct,
                    ProductName = k.Product != null ? k.Product.Name : null,
                    Title = k.Title,
                    Content = k.Content,
                    Category = k.Category,
                    SourceDocument = k.SourceDocument,
                    HasEmbedding = k.Embedding != null && k.Embedding != "",
                    CreatedAt = k.CreatedAt,
                    UpdatedAt = k.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<ProductKnowledgeDTO?> GetItemAsync(int id)
        {
            return await _context.ProductKnowledge.AsNoTracking()
                .Where(k => k.Id == id)
                .Select(k => new ProductKnowledgeDTO
                {
                    Id = k.Id,
                    IdProduct = k.IdProduct,
                    ProductName = k.Product != null ? k.Product.Name : null,
                    Title = k.Title,
                    Content = k.Content,
                    Category = k.Category,
                    SourceDocument = k.SourceDocument,
                    HasEmbedding = k.Embedding != null && k.Embedding != "",
                    CreatedAt = k.CreatedAt,
                    UpdatedAt = k.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ProductKnowledgeDTO> SaveAsync(ProductKnowledge item)
        {
            var isNew = item.Id <= 0;

            ProductKnowledge entity;
            if (isNew)
            {
                entity = new ProductKnowledge { CreatedAt = DateTime.UtcNow };
                _context.ProductKnowledge.Add(entity);
            }
            else
            {
                entity = await _context.ProductKnowledge.FirstOrDefaultAsync(k => k.Id == item.Id)
                         ?? throw new InvalidOperationException($"Voce KB {item.Id} non trovata");
                entity.UpdatedAt = DateTime.UtcNow;
            }

            entity.IdProduct = item.IdProduct;
            entity.Title = item.Title?.Trim() ?? string.Empty;
            entity.Content = item.Content?.Trim() ?? string.Empty;
            entity.Category = string.IsNullOrWhiteSpace(item.Category) ? null : item.Category.Trim();
            entity.Embedding = await TryBuildEmbeddingAsync(entity.Title, entity.Content);

            await _context.SaveChangesAsync();

            // Ricarica per ottenere il nome prodotto
            return await GetItemAsync(entity.Id) ?? ToDto(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _context.ProductKnowledge.FirstOrDefaultAsync(k => k.Id == id);
            if (entity == null)
                return false;

            _context.ProductKnowledge.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<KnowledgeEmbeddingStats> GenerateMissingEmbeddingsAsync(int batchSize = 20)
        {
            var pending = await _context.ProductKnowledge
                .Where(k => k.Embedding == null || k.Embedding == "")
                .OrderBy(k => k.Id)
                .Take(batchSize)
                .ToListAsync();

            int processed = 0;
            foreach (var entity in pending)
            {
                var embedding = await TryBuildEmbeddingAsync(entity.Title, entity.Content);
                if (!string.IsNullOrEmpty(embedding))
                {
                    entity.Embedding = embedding;
                    processed++;
                }
            }

            if (processed > 0)
                await _context.SaveChangesAsync();

            var total = await _context.ProductKnowledge.CountAsync();
            var withEmbedding = await _context.ProductKnowledge.CountAsync(k => k.Embedding != null && k.Embedding != "");

            return new KnowledgeEmbeddingStats
            {
                Processed = processed,
                Remaining = total - withEmbedding,
                TotalWithEmbedding = withEmbedding,
                Total = total
            };
        }

        public async Task<KnowledgeImportResult> ImportDocumentAsync(byte[] fileBytes, string fileName, int? idProduct, string? category)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return new KnowledgeImportResult { Chunks = 0, Message = "File vuoto." };

            var text = DocumentTextExtractor.Extract(fileBytes, fileName);
            var chunks = ChunkText(text, MaxChunkLength);

            if (chunks.Count == 0)
                return new KnowledgeImportResult { Chunks = 0, Message = "Nessun testo estratto dal documento (potrebbe essere un PDF scansionato)." };

            // Cap di sicurezza sul numero di blocchi per un singolo import
            var truncated = false;
            if (chunks.Count > MaxChunksPerImport)
            {
                chunks = chunks.Take(MaxChunksPerImport).ToList();
                truncated = true;
            }

            // Embedding in batch (best-effort: se fallisce, le voci restano senza embedding)
            List<float[]>? embeddings = null;
            var embeddingsOk = false;
            try
            {
                embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);
                embeddingsOk = embeddings != null && embeddings.Count == chunks.Count;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(KnowledgeService), nameof(ImportDocumentAsync), EventsTypes.Warning,
                    $"Embedding batch import non riuscito: {ex.Message}");
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var cat = string.IsNullOrWhiteSpace(category) ? "Documento" : category.Trim();
            var now = DateTime.UtcNow;

            for (int i = 0; i < chunks.Count; i++)
            {
                _context.ProductKnowledge.Add(new ProductKnowledge
                {
                    IdProduct = idProduct,
                    Title = chunks.Count > 1 ? $"{baseName} (parte {i + 1}/{chunks.Count})" : baseName,
                    Content = chunks[i],
                    Category = cat,
                    SourceDocument = fileName,
                    Embedding = embeddingsOk ? JsonSerializer.Serialize(embeddings![i]) : null,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();

            var message = embeddingsOk
                ? (truncated ? $"Documento lungo: importati i primi {chunks.Count} blocchi." : null)
                : "Voci salvate senza embedding: usa il bottone \"Genera embedding\".";

            return new KnowledgeImportResult
            {
                Chunks = chunks.Count,
                EmbeddingsGenerated = embeddingsOk,
                Message = message
            };
        }

        public async Task<List<KnowledgeMatch>> SearchSimilarAsync(
            float[] queryEmbedding, ICollection<int> relevantProductIds, int top, double minSimilarity)
        {
            var matches = new List<(KnowledgeMatch match, double score)>();

            if (queryEmbedding == null || queryEmbedding.Length == 0)
                return new List<KnowledgeMatch>();

            var entries = await _context.ProductKnowledge.AsNoTracking()
                .Where(k => k.Embedding != null && k.Embedding != "")
                .Select(k => new
                {
                    k.Id,
                    k.Title,
                    k.Content,
                    k.Category,
                    k.IdProduct,
                    ProductName = k.Product != null ? k.Product.Name : null,
                    Embedding = k.Embedding
                })
                .ToListAsync();

            var relevant = relevantProductIds ?? Array.Empty<int>();

            foreach (var e in entries)
            {
                try
                {
                    var embedding = JsonSerializer.Deserialize<float[]>(e.Embedding!);
                    if (embedding == null || embedding.Length == 0)
                        continue;

                    var similarity = _embeddingService.CalculateCosineSimilarity(queryEmbedding, embedding);
                    var percentage = _embeddingService.CosineSimilarityToPercentage(similarity);

                    var productMatch = e.IdProduct.HasValue && relevant.Contains(e.IdProduct.Value);

                    // Voci del modello coinvolto: soglia più permissiva; altre: soglia piena
                    if (percentage < minSimilarity && !(productMatch && percentage >= minSimilarity - ProductMatchBonus))
                        continue;

                    matches.Add((new KnowledgeMatch
                    {
                        Id = e.Id,
                        Title = e.Title,
                        Content = e.Content,
                        ProductName = e.ProductName,
                        Category = e.Category,
                        SimilarityPercentage = Math.Round(percentage, 2),
                        ProductMatch = productMatch
                    }, percentage + (productMatch ? ProductMatchBonus : 0)));
                }
                catch (Exception ex)
                {
                    await _logEventService.RegisterAsync(nameof(KnowledgeService), nameof(SearchSimilarAsync), EventsTypes.Warning,
                        $"Embedding KB non valido per la voce {e.Id}: {ex.Message}");
                }
            }

            return matches
                .OrderByDescending(m => m.score)
                .Take(Math.Max(1, top))
                .Select(m => m.match)
                .ToList();
        }

        // ---------- Helper ----------

        /// <summary>
        /// Suddivide il testo in blocchi coerenti (per paragrafo) entro <paramref name="maxLen"/> caratteri.
        /// I paragrafi troppo lunghi vengono spezzati in modo forzato.
        /// </summary>
        private static List<string> ChunkText(string text, int maxLen)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var paragraphs = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            var sb = new StringBuilder();
            foreach (var p in paragraphs)
            {
                if (p.Length > maxLen)
                {
                    if (sb.Length > 0) { result.Add(sb.ToString().Trim()); sb.Clear(); }
                    for (int i = 0; i < p.Length; i += maxLen)
                        result.Add(p.Substring(i, Math.Min(maxLen, p.Length - i)).Trim());
                    continue;
                }

                if (sb.Length + p.Length + 1 > maxLen)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }

                sb.Append(p).Append('\n');
            }

            if (sb.Length > 0)
                result.Add(sb.ToString().Trim());

            return result.Where(c => c.Length > 0).ToList();
        }

        private async Task<string?> TryBuildEmbeddingAsync(string title, string content)
        {
            var text = $"{title}\n{content}".Trim();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
                return embedding.Length > 0 ? JsonSerializer.Serialize(embedding) : null;
            }
            catch (Exception ex)
            {
                // La voce viene salvata comunque; l'embedding potrà essere generato dopo (batch)
                await _logEventService.RegisterAsync(nameof(KnowledgeService), nameof(TryBuildEmbeddingAsync), EventsTypes.Warning,
                    $"Generazione embedding KB non riuscita: {ex.Message}");
                return null;
            }
        }

        private static ProductKnowledgeDTO ToDto(ProductKnowledge k) => new()
        {
            Id = k.Id,
            IdProduct = k.IdProduct,
            ProductName = k.Product != null ? k.Product.Name : null,
            Title = k.Title,
            Content = k.Content,
            Category = k.Category,
            SourceDocument = k.SourceDocument,
            HasEmbedding = k.Embedding != null && k.Embedding != "",
            CreatedAt = k.CreatedAt,
            UpdatedAt = k.UpdatedAt
        };
    }
}
