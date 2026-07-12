using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;

namespace CRM.Server.Services
{
    /// <summary>
    /// Gestione della base di conoscenza (voci associate ai modelli/prodotti) e ricerca semantica
    /// usata dall'assistente ticket.
    /// </summary>
    public interface IKnowledgeService
    {
        Task<List<ProductKnowledgeDTO>> GetListAsync(ProductKnowledgeFilter? filter = null);

        Task<ProductKnowledgeDTO?> GetItemAsync(int id);

        /// <summary>Crea o aggiorna una voce e ne (ri)genera l'embedding.</summary>
        Task<ProductKnowledgeDTO> SaveAsync(ProductKnowledge item);

        Task<bool> DeleteAsync(int id);

        /// <summary>Elimina in blocco tutte le parti di un documento importato. Restituisce il numero di parti rimosse.</summary>
        Task<int> DeleteDocumentAsync(Guid groupId);

        /// <summary>Rigenera l'embedding di tutte le parti di un documento importato. Restituisce il numero di parti rielaborate.</summary>
        Task<int> RegenerateDocumentEmbeddingsAsync(Guid groupId);

        /// <summary>Genera gli embedding mancanti per le voci esistenti (batch).</summary>
        Task<KnowledgeEmbeddingStats> GenerateMissingEmbeddingsAsync(int batchSize = 20);

        /// <summary>
        /// Importa un documento (PDF/Word/testo): ne estrae il testo, lo suddivide in blocchi e crea
        /// una voce di conoscenza per blocco (con embedding), opzionalmente associata a un modello.
        /// </summary>
        Task<KnowledgeImportResult> ImportDocumentAsync(byte[] fileBytes, string fileName, int? idProduct, string? category);

        /// <summary>
        /// Ricerca semantica sulle voci di conoscenza. Le voci del modello coinvolto nei ticket simili
        /// (<paramref name="relevantProductIds"/>) ricevono un bonus di ranking.
        /// </summary>
        Task<List<KnowledgeMatch>> SearchSimilarAsync(
            float[] queryEmbedding, ICollection<int> relevantProductIds, int top, double minSimilarity);
    }
}
