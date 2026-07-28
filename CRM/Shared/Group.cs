
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Group
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Nome")]
        [Required]
        public string Name { get; set; }
        
        [Display(Name = "Descrizione")]
        public string Description { get; set; }

        /// <summary>
        /// Competenze del gruppo raccontate all'AI che smista i ticket: cosa tratta, cosa NON tratta,
        /// prodotti e parole chiave tipiche. E' l'unica fonte su cui il modello sceglie il gruppo,
        /// quindi vale la pena curarla: senza, il gruppo resta un candidato "muto".
        /// </summary>
        [Display(Name = "Competenze per lo smistamento AI")]
        [MaxLength(2000)]
        public string? AiRoutingHints { get; set; }

        public virtual ICollection<ApplicationUser> Users { get; set; }

        public virtual ICollection<TicketType> TicketTypes { get; set; }
    }

    public class GroupFilter: PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int? IdTicketType { get; set; }
    }
}
