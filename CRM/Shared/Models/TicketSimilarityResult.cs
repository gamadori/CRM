using System;

namespace CRM.Shared.Models
{
    /// <summary>
    /// Risultato della ricerca semantica con similarità AI
    /// </summary>
    public class TicketSimilarityResult
    {
        /// <summary>
        /// ID del ticket trovato
        /// </summary>
        public int TicketId { get; set; }

        /// <summary>
        /// Numero/Codice del ticket
        /// </summary>
        public string TicketNumber { get; set; }

        /// <summary>
        /// Titolo del ticket
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Descrizione del ticket
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Nome del cliente
        /// </summary>
        public string CustomerName { get; set; }

        /// <summary>
        /// Percentuale di similarità (0-100)
        /// </summary>
        public double SimilarityPercentage { get; set; }

        /// <summary>
        /// Score di similarità coseno (-1 a 1)
        /// </summary>
        public double CosineSimilarity { get; set; }

        /// <summary>
        /// Data di chiusura del ticket
        /// </summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>
        /// Soluzione applicata al ticket
        /// </summary>
        public string Solution { get; set; }

        /// <summary>
        /// Categoria del ticket
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Priorità del ticket
        /// </summary>
        public string Priority { get; set; }
    }
}
