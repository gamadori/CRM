using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Voto dell'operatore su una risposta dell'assistente AI.</summary>
    public enum AssistantFeedbackVote
    {
        Down = -1,
        Up = 1
    }

    /// <summary>
    /// Registro di una domanda/risposta dell'assistente AI, con il feedback (facoltativo)
    /// dell'operatore. Usato per valutare e migliorare la qualità dell'assistente.
    /// </summary>
    [Table("AssistantChatLogs")]
    public class AssistantChatLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Question { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        /// <summary>JSON compatto dei ticket citati come fonte (id, numero, similarità).</summary>
        public string? ReferencedTicketsJson { get; set; }

        /// <summary>Ticket di contesto da cui è partita la chat (se presente).</summary>
        public int? IdTicket { get; set; }

        /// <summary>Modello/prodotto di contesto (se presente).</summary>
        public int? IdProduct { get; set; }

        /// <summary>Operatore che ha posto la domanda.</summary>
        [ForeignKey(nameof(User))]
        [MaxLength(450)]
        public string? IdUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Voto dell'operatore: null = nessun voto.</summary>
        public AssistantFeedbackVote? Feedback { get; set; }

        /// <summary>Commento facoltativo (tipicamente sul voto negativo).</summary>
        public string? FeedbackComment { get; set; }

        public DateTime? FeedbackAt { get; set; }

        public virtual ApplicationUser? User { get; set; }
    }

    /// <summary>Filtro per la consultazione dei log dell'assistente.</summary>
    public class AssistantChatLogFilter : PagingParameterModel
    {
        /// <summary>Ricerca testuale in domanda/risposta.</summary>
        public string? Search { get; set; }

        /// <summary>Filtro voto: null = tutti, 1 = positivi, -1 = negativi, 0 = senza voto.</summary>
        public int? Vote { get; set; }
    }
}
