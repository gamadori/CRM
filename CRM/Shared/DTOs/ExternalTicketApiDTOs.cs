using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    // I DTO delle chiavi non stanno piu' qui: sono confluiti in ApiKeyDTO / ApiKeyCreateRequest /
    // ApiKeyCreateResponse, condivisi con backup macchina e app fiera. Qui restano i ticket.

    public class ExternalTicketCreateRequest
    {
        // Facoltativo: senza tipo il CRM assegna il tipo di assistenza predefinito
        // (ExternalTickets:DefaultTicketTypeId, altrimenti il primo tipo aperto ai clienti).
        // Un pannello macchina non conosce gli ID del CRM e non deve conoscerli.
        public int? IdType { get; set; }

        public int? IdArticle { get; set; }

        // Matricola della macchina: in alternativa a IdArticle, il CRM risale all'articolo
        // dell'azienda della chiave. E' cio' che una macchina sa di se'.
        public string? SerialNumber { get; set; }

        public int? IdProduct { get; set; }

        public int? IdProject { get; set; }

        public int? IdContact { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        public TicketPriorities Priority { get; set; } = TicketPriorities.Medium;

        public DateTime? Date { get; set; }

        public DateTime? DateEnd { get; set; }

        // La scadenza non si manda da fuori: la calcola il CRM dai giorni concessi dal tipo di
        // ticket. Accettarla era una promessa falsa - il riallineamento delle scadenze la
        // riscriveva alla prima apertura di un elenco, quindi il valore chiesto durava finche'
        // qualcuno non guardava. In lettura la scadenza c'e' ancora: e' cio' che il chiamante deve
        // sapere, ed e' quella vera.

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

    /// <summary>
    /// Un file allegato a un ticket esterno dopo la sua apertura: per esempio lo storico
    /// allarmi che il pannello macchina manda insieme alla richiesta di assistenza.
    /// </summary>
    public class ExternalTicketAttachmentResponse
    {
        public int IdTicket { get; set; }

        public int IdAttachment { get; set; }

        public int IdFile { get; set; }

        public string FileName { get; set; } = string.Empty;

        public long Size { get; set; }
    }
}
