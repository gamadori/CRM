using System;

namespace CRM.Shared.DTOs
{
    /// <summary>Voce di log dell'assistente per la pagina di consultazione admin.</summary>
    public class AssistantChatLogDTO
    {
        public int Id { get; set; }

        public string Question { get; set; } = string.Empty;

        public string Answer { get; set; } = string.Empty;

        public string? UserName { get; set; }

        public int? IdTicket { get; set; }

        public int? IdProduct { get; set; }

        /// <summary>Numero di ticket citati come fonte.</summary>
        public int ReferencedCount { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Voto: -1 negativo, 1 positivo, null nessun voto.</summary>
        public int? Feedback { get; set; }

        public string? FeedbackComment { get; set; }

        public DateTime? FeedbackAt { get; set; }
    }
}
