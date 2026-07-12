using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>
    /// Voce della base di conoscenza (manuale, guasto tipico, procedura, FAQ) associata
    /// opzionalmente a un modello di macchina (<see cref="Product"/>). Se <see cref="IdProduct"/>
    /// è null la voce è considerata conoscenza generale, valida per tutti i modelli.
    /// </summary>
    [Table("ProductKnowledge")]
    public class ProductKnowledge
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Modello di riferimento; null = conoscenza generale.</summary>
        [ForeignKey(nameof(Product))]
        public int? IdProduct { get; set; }

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Category { get; set; }

        /// <summary>Nome del documento sorgente, se la voce deriva da un import (PDF/Word); altrimenti null.</summary>
        [MaxLength(300)]
        public string? SourceDocument { get; set; }

        /// <summary>
        /// Identificatore dell'import di provenienza: tutte le parti (chunk) dello stesso documento
        /// caricato condividono lo stesso valore. Null per le voci create manualmente (non chunkate).
        /// Permette di gestire il documento come un'unica unità (raggruppamento, elimina/rigenera).
        /// </summary>
        public Guid? DocumentGroupId { get; set; }

        /// <summary>Embedding del testo (Title + Content) serializzato come JSON di float[].</summary>
        public string? Embedding { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual Product? Product { get; set; }
    }

    /// <summary>Filtro per l'elenco/gestione delle voci di conoscenza.</summary>
    public class ProductKnowledgeFilter : PagingParameterModel
    {
        public int? IdProduct { get; set; }

        /// <summary>Ricerca testuale in titolo/contenuto.</summary>
        public string? Search { get; set; }

        public string? Category { get; set; }
    }
}
