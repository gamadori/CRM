namespace CRM.Shared.Models
{
    /// <summary>Esito dell'import di un documento nella base di conoscenza.</summary>
    public class KnowledgeImportResult
    {
        /// <summary>Numero di voci (chunk) create dal documento.</summary>
        public int Chunks { get; set; }

        /// <summary>True se gli embedding sono stati generati durante l'import.</summary>
        public bool EmbeddingsGenerated { get; set; }

        public string? Message { get; set; }
    }
}
