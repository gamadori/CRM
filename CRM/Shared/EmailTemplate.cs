using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum EmailsTypes
    {
        [Display(Name="Conferma Email")]
        ConfirmEmail,
        
        [Display(Name = "Conferma Registrazione")]
        ConfirmRegister,
        
        [Display(Name = "Invito Registrazione")]
        Invito,
        
        [Display(Name ="Invio Documento")]
        InvioDocumento,
        
        [Display(Name ="Avviso Apertura Ticket")]
        NoticeNewTicket,
        
        [Display(Name = "Avviso Nuovo Ticket da Assegnare")]
        NoticeNewTicketToBeAssigned,

        [Display(Name = "Avviso Assegnato Ticket")]
        NoticeTicketAssigned,

        [Display(Name = "Nuovo Allegato")]
        NewAttachment,

        [Display(Name ="Nuovo Utente")]
        NoticeNewUser,

        [Display(Name = "Nuovo Messaggio di Chat")]
        NoticeNewChatMessage,

        [Display(Name = "Conferma Firma")]
        SignatureConfirm,

        [Display(Name = "Promemoria Attività")]
        NoticeReminder,

        [Display(Name = "Ticket bloccato")]
        TicketBlocked,

        [Display(Name = "Ticket sbloccato")]
        TicketUnblocked,

        [Display(Name = "Reset Password")]
        PasswordReset,

        /// <summary>Link con cui il cliente firma il verbale da remoto.</summary>
        [Display(Name = "Richiesta Firma da Remoto")]
        SignatureRequest,

    }

    [Table("EmailTemplates")]
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(EmailTemplate.Tipo), ResourceType = typeof(Resources.Models.EmailTemplates))]
        public EmailsTypes Tipo { get; set; }

        /// <summary>Codice lingua del template (es. "it", "en"). Consente una versione per lingua per ogni tipo.</summary>
        [Display(Name = "Lingua")]
        [MaxLength(10)]
        public string? Language { get; set; }

        [Required]
        [Display(Name = nameof(EmailTemplate.Subject), ResourceType = typeof(Resources.Models.EmailTemplates))]
        public string Subject { get; set; }

        [Required]
        [MaxLength(20000)]
        public string Body { get; set; }

        [Display(Name = nameof(EmailTemplate.Logo), ResourceType = typeof(Resources.Models.EmailTemplates))]
        [ForeignKey("Logo")]
        
        public int? IdLogo { get; set; }

        public virtual Logo Logo { get; set; }

    }

    public class EmailTemplateFilter: PagingParameterModel
    {
        public EmailsTypes? Tipo { get; set; }

        public string Subject { get; set; }
    }
}
