using System.Collections.Generic;

namespace CRM.Shared.Models
{
    /// <summary>
    /// Richiesta di ricerca semantica AI
    /// </summary>
    public class SemanticSearchRequest
    {
        /// <summary>
        /// Descrizione del problema da cercare
        /// </summary>
        public string ProblemDescription { get; set; }

        /// <summary>
        /// Numero massimo di risultati da restituire
        /// </summary>
        public int TopResults { get; set; } = 10;

        /// <summary>
        /// Soglia minima di similarità (0-100)
        /// </summary>
        public double MinSimilarityThreshold { get; set; } = 60.0;

        /// <summary>
        /// Filtro per categoria (opzionale)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Solo ticket chiusi
        /// </summary>
        public bool OnlyClosedTickets { get; set; } = true;
    }

    /// <summary>
    /// Risposta della ricerca semantica
    /// </summary>
    public class SemanticSearchResponse
    {
        /// <summary>
        /// Ticket simili trovati
        /// </summary>
        public List<TicketSimilarityResult> Results { get; set; } = new();

        /// <summary>
        /// Numero totale di ticket analizzati
        /// </summary>
        public int TotalAnalyzed { get; set; }

        /// <summary>
        /// Tempo di elaborazione in millisecondi
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Query embedding generata
        /// </summary>
        public bool EmbeddingGenerated { get; set; }

        /// <summary>
        /// Messaggio informativo o di errore
        /// </summary>
        public string? Message { get; set; }
    }
}
