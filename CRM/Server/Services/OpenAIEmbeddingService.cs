using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per gestire embeddings e ricerca semantica con OpenAI
    /// </summary>
    public class OpenAIEmbeddingService
    {
        private readonly ILogger<OpenAIEmbeddingService> _logger;
        private readonly OpenAIClient _openAIClient;
        private readonly string _embeddingModel;

        public OpenAIEmbeddingService(
            IConfiguration configuration,
            ILogger<OpenAIEmbeddingService> logger)
        {
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_OPENAI_API_KEY_HERE")
            {
                _logger.LogWarning("OpenAI API Key non configurata. Configurare OpenAI:ApiKey in appsettings.json");
                throw new InvalidOperationException("OpenAI API Key non configurata");
            }

            _openAIClient = new OpenAIClient(apiKey);
            _embeddingModel = configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";

            _logger.LogInformation("OpenAI Embedding Service inizializzato con modello: {Model}", _embeddingModel);
        }

        /// <summary>
        /// Genera embedding per un testo
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Tentativo di generare embedding per testo vuoto");
                    return Array.Empty<float>();
                }

                var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingModel);
                var response = await embeddingClient.GenerateEmbeddingAsync(text);

                var embedding = response.Value.ToFloats().ToArray();

                _logger.LogDebug("Embedding generato: {Dimensions} dimensioni", embedding.Length);

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la generazione dell'embedding");
                throw;
            }
        }

        /// <summary>
        /// Genera embeddings per più testi in batch
        /// </summary>
        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
        {
            try
            {
                if (texts == null || !texts.Any())
                {
                    return new List<float[]>();
                }

                var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingModel);
                var response = await embeddingClient.GenerateEmbeddingsAsync(texts);

                var embeddings = response.Value
                    .Select(e => e.ToFloats().ToArray())
                    .ToList();

                _logger.LogInformation("Generati {Count} embeddings", embeddings.Count);

                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la generazione degli embeddings batch");
                throw;
            }
        }

        /// <summary>
        /// Calcola la similarità coseno tra due vettori
        /// </summary>
        public double CalculateCosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1 == null || vector2 == null || vector1.Length != vector2.Length)
            {
                _logger.LogWarning("Vettori non validi per calcolo similarità");
                return 0.0;
            }

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }

        /// <summary>
        /// Converte similarità coseno in percentuale (0-100)
        /// </summary>
        public double CosineSimilarityToPercentage(double cosineSimilarity)
        {
            // Per embeddings OpenAI, cosine similarity è quasi sempre >= 0
            // Normalizziamo direttamente 0-1 -> 0-100% (più intuitivo)
            // Valori negativi (molto rari) vengono trattati come 0%
            if (cosineSimilarity < 0)
            {
                return 0.0;
            }

            // Moltiplica direttamente per 100 (range 0-1 -> 0-100%)
            return Math.Min(cosineSimilarity * 100.0, 100.0);
        }
    }
}
