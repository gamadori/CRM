using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class UsersFilterModel: PagingParameterModel
    {
        [Display(Name = nameof(ApplicationUser.Name), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Name { get; set; }

        [Display(Name = nameof(ApplicationUser.Surname), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string SurName { get; set; }
        public string Email { get; set; }
        public int? IdCompany { get; set; }
        public int? IdCustomer { get; set; }
        public int? IdGroup { get; set; }
        public int? IdTicketType { get; set; }
        public string NameComplete { get; set; }

        [Display(Name = nameof(ApplicationUser.Enabled), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool Enabled { get; set; }

        [Display(Name = nameof(ApplicationUser.AdminConfirmed), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool? AdminConfirmed { get; set; }

        public int? TicketTypeToAssign { get; set; }

        public int? IdTicketToAssign { get; set; }

        public int? IdTicketAssigned { get; set; }

        public int? IdProject { get; set; } = null;

        public int? IdProjectParent { get; set; } = null;
    }
}
