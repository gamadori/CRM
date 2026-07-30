using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketChat
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        /// <summary>Autore interno. Null per i messaggi ricevuti dall'esterno (es. risposta email del cliente).</summary>
        [ForeignKey("User")]
        public string IdUser { get; set; }

        /// <summary>
        /// Mittente esterno (indirizzo email) quando il messaggio non proviene da un utente interno:
        /// usato al posto del nome utente nella chat.
        /// </summary>
        public string? ExternalSender { get; set; }

        public DateTime Date { get; set; }

        public string Message { get; set; }

        [ForeignKey("AttachmentFile")]
        public int? IdAttachmentFile { get; set; }

        /// <summary>
        /// Messaggio eliminato dall'utente che lo ha inviato (stile WhatsApp):
        /// la riga resta ma testo/allegato vengono rimossi.
        /// </summary>
        public bool Deleted { get; set; }

        public virtual Ticket Ticket { get; set; }

        public virtual ApplicationUser User { get; set; }

        public virtual AttachmentFile? AttachmentFile { get; set; }

        public ICollection<TicketChatRead> TicketChatReads { get; set; }
    }

    public class TicketChatFilterModel : PagingParameterModel
    {
        public int? IdTicket { get; set; }

        public string IdUser { get; set; }


    }

    public enum TypeMessage
    {
        TicketMsg,
        Receved,
        Sended,
       
    }


    public class TicketChatViewModel
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int IdTicket { get; set; }

        public string IdUser { get; set; }

        public string UserName { get; set; }

        [Required]
        public string Message { get; set; }

        //public bool Receved { get; set; }

        public TypeMessage TypeMessage { get; set; }

        public bool Read { get; set; }

        public string Color { get; set; }

        public bool Last { get; set; } = false;

        public int? AttachmentFileId { get; set; }
        public int? AttachmentId { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }

        /// <summary>Messaggio eliminato: mostra solo il segnaposto con data/ora.</summary>
        public bool Deleted { get; set; }

        /// <summary>L'utente corrente è il mittente e può eliminare il messaggio.</summary>
        public bool CanDelete { get; set; }
    }

    /// <summary>
    /// Una conversazione con messaggi da leggere, vista dall'utente corrente: anteprima
    /// dell'ultimo messaggio piu' il contesto del ticket a cui appartiene.
    /// Raggruppa per ticket e non per messaggio perche' aprire la chat segna letti tutti i
    /// messaggi di quel ticket: una riga per messaggio sparirebbe a gruppi, in modo poco leggibile.
    /// </summary>
    public class UnreadChatModel
    {
        /// <summary>Ultimo messaggio non letto: la chat viene aperta posizionandosi su di lui.</summary>
        public int IdChat { get; set; }

        public int IdTicket { get; set; }

        public string? TicketNumber { get; set; }

        /// <summary>Oggetto del ticket, per capire di cosa si parla senza aprirlo.</summary>
        public string? TicketDescription { get; set; }

        public string? CompanyName { get; set; }

        /// <summary>Autore del messaggio: utente interno, contatto del cliente o mittente email.</summary>
        public string? SenderName { get; set; }

        /// <summary>Id dell'autore quando e' un utente: serve all'avatar.</summary>
        public string? IdSender { get; set; }

        public DateTime Date { get; set; }

        /// <summary>Anteprima del testo, gia' troncata dal server.</summary>
        public string? Preview { get; set; }

        public bool Deleted { get; set; }

        public bool HasAttachment { get; set; }

        public string? AttachmentFileName { get; set; }

        /// <summary>Messaggi ancora da leggere su questo ticket.</summary>
        public int UnreadCount { get; set; }

        public bool TicketClosed { get; set; }

        public bool TicketBlocked { get; set; }
    }

    public class ChatFileUploadResult
    {
        public int Id { get; set; }
        public int AttachmentId { get; set; }
        public string Name { get; set; } = "";
        public string ContentType { get; set; } = "";
    }
}
