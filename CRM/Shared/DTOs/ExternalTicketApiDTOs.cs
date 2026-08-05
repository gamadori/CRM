using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    // I DTO delle chiavi non stanno piu' qui: sono confluiti in ApiKeyDTO / ApiKeyCreateRequest /
    // ApiKeyCreateResponse, condivisi con backup macchina e app fiera. Qui restano i ticket.

    public class ExternalTicketCreateRequest
    {
        [Required]
        public int IdType { get; set; }

        public int? IdArticle { get; set; }

        public int? IdProduct { get; set; }

        public int? IdProject { get; set; }

        public int? IdContact { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        public TicketPriorities Priority { get; set; } = TicketPriorities.Medium;

        public DateTime? Date { get; set; }

        public DateTime? DateEnd { get; set; }

        public DateTime? DateExpired { get; set; }

        public string? ExternalReference { get; set; }
    }

    public class ExternalTicketResponse
    {
        public int Id { get; set; }

        public string? Numero { get; set; }

        public int IdCompany { get; set; }

        public string? Company { get; set; }

        public int IdType { get; set; }

        public int? IdState { get; set; }

        public string? State { get; set; }

        public string? StateColor { get; set; }

        public int Progress { get; set; }

        public bool Closed { get; set; }

        public DateTime DateOpened { get; set; }

        public DateTime? Date { get; set; }

        public DateTime? DateEnd { get; set; }

        public DateTime? DateExpired { get; set; }

        public DateTime? DateClosed { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? OperationalSummary { get; set; }

        public string? CloseDescription { get; set; }
    }
}
