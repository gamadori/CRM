using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    
    public class TicketIntervention
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Ticket")]
        public int IdTicket { get; set; }

        [ForeignKey("User")]
        [Display(Name = "User")]
        [Required]
        public string IdUser { get; set; }

       
        [Display(Name = "Tipo Supporto")]
        public int SupportType { get; set; }

        [Required]
        [Display(Name = "Attività svolte")]
        public string Activities { get; set; }

        [Display(Name = "Parti Sostituite o Montate")]
        public string MountedParts { get; set; }

        [Display(Name = "Nota")]
        public string Note { get; set; }

        [Display(Name = "Data e Orario di Inizio")]
        public DateTime StartDateTime { get; set; }

        [Display(Name = "Orario di Fine")]
        public DateTime EndDateTime { get; set; }


        public bool HasAttachments { get; set; }

        [NotMapped]
        public bool AttachmentExist { get; set; }

        [Display(Name = "Minute")]
        public int Minute { get; set; }


        [Display(Name = "Tipi di Intervento")]
        [NotMapped]
        public List<int> InterventionsTypesId { get; set; } = new List<int>();

        [NotMapped]
        public int Permits { get; set; }

        [NotMapped]
        public string UserName { get; set; }

        [NotMapped]
        public string SupportTypeDesc { get; set; }

        [NotMapped]
        public List<TicketInterventionArticleModel> InterventionArticles { get; set; } = new List<TicketInterventionArticleModel>();
        public virtual Ticket Ticket { get; set; }

        public virtual ApplicationUser User { get; set; }

        [Display(Name = "Tipi di Intervento")]
        public virtual ICollection<InterventionType> TicketInterventionsTypes { get; set; }

        [Display(Name = "Dispositivi")]
        public virtual ICollection<TicketInterventionArticle> TicketInterventionArticles { get; set; }

        [Display(Name = "Orari")]
        public virtual ICollection<TicketInterventionTime> TicketInterventionTime { get; set; }
    }

    public class TicketInterventionFilter: PagingParameterModel
    {
        public int? IdTicket { get; set; }

        public string IdUser { get; set; }

        public DateTime? DateFrom { get; set; }

        public DateTime? DateTo { get; set; }

        public int? SupportType { get; set; }
    }
}
