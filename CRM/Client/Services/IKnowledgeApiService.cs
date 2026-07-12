using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>Client per la gestione della base di conoscenza (API ProductKnowledge).</summary>
    public interface IKnowledgeApiService
    {
        Task<List<ProductKnowledgeDTO>> GetList(ProductKnowledgeFilter? filter = null);

        Task<ProductKnowledgeDTO?> Get(int id);

        Task<ProductKnowledgeDTO?> Save(ProductKnowledge item);

        Task<bool> Delete(int id);

        /// <summary>Elimina un intero documento importato (tutte le sue parti). Restituisce il numero di parti rimosse, o null in caso di errore.</summary>
        Task<int?> DeleteDocument(System.Guid groupId);

        /// <summary>Rigenera l'embedding di tutte le parti di un documento importato. Restituisce il numero di parti rielaborate, o null in caso di errore.</summary>
        Task<int?> RegenerateDocumentEmbeddings(System.Guid groupId);

        Task<KnowledgeEmbeddingStats?> GenerateEmbeddings(int batchSize = 20);

        Task<KnowledgeImportResult?> ImportDocument(Stream fileStream, string fileName, int? idProduct, string? category);
    }
}
