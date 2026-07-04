using System;

namespace CRM.Shared.DTOs
{
    /// <summary>DTO di una voce di base di conoscenza (senza l'embedding grezzo).</summary>
    public class ProductKnowledgeDTO
    {
        public int Id { get; set; }

        public int? IdProduct { get; set; }

        /// <summary>Nome del modello associato, oppure null se conoscenza generale.</summary>
        public string? ProductName { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string? Category { get; set; }

        /// <summary>Documento sorgente (se la voce deriva da un import), altrimenti null.</summary>
        public string? SourceDocument { get; set; }

        /// <summary>True se la voce ha già un embedding calcolato.</summary>
        public bool HasEmbedding { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
