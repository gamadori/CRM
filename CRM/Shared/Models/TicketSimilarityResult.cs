using System;

namespace CRM.Shared.Models
{
    /// <summary>
    /// Risultato della ricerca semantica con similarit� AI
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
        /// Percentuale di similarit� (0-100)
        /// </summary>
        public double SimilarityPercentage { get; set; }

        /// <summary>
        /// Score di similarit� coseno (-1 a 1)
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
        /// Priorit� del ticket
        /// </summary>
        public string Priority { get; set; }

        /// <summary>
        /// True se l'utente corrente può accedere a questo ticket (mostra il link solo in tal caso).
        /// </summary>
        public bool CanAccess { get; set; } = true;
    }
}
