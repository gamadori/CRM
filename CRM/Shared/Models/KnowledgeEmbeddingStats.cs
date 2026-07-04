namespace CRM.Shared.Models
{
    /// <summary>Statistiche sulla generazione degli embedding della base di conoscenza.</summary>
    public class KnowledgeEmbeddingStats
    {
        public int Processed { get; set; }
        public int Remaining { get; set; }
        public int TotalWithEmbedding { get; set; }
        public int Total { get; set; }
    }
}
