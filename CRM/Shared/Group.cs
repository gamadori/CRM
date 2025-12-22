
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
