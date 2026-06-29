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

        [ForeignKey("User")]
        public string IdUser { get; set; }

        public DateTime Date { get; set; }

        public string Message { get; set; }

        [ForeignKey("AttachmentFile")]
        public int? IdAttachmentFile { get; set; }

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
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }
    }

    public class ChatFileUploadResult
    {
        public int Id { get; set; }
        public int AttachmentId { get; set; }
        public string Name { get; set; } = "";
        public string ContentType { get; set; } = "";
    }
}
