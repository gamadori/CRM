namespace CRM.Shared.Models
{
    /// <summary>
    /// Voce di base di conoscenza rilevante per una domanda, con relativo punteggio di similarità.
    /// </summary>
    public class KnowledgeMatch
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        /// <summary>Modello associato, oppure null se conoscenza generale.</summary>
        public string? ProductName { get; set; }

        public string? Category { get; set; }

        public double SimilarityPercentage { get; set; }

        /// <summary>True se la voce è del modello coinvolto nei ticket simili (match sul modello).</summary>
        public bool ProductMatch { get; set; }
    }
}
