using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class DealModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(Deal.Name), ResourceType = typeof(Resources.Models.Deal))]
        public string Name { get; set; }

        [Display(Name = nameof(Deal.Date), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime Date { get; set; }

        [Display(Name = nameof(Deal.Company), ResourceType = typeof(Resources.Models.Deal))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Deal.Contact), ResourceType = typeof(Resources.Models.Deal))]
        public int? IdContact { get; set; }

        [Display(Name = nameof(Deal.Amount), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Amount { get; set; }

        [Display(Name = nameof(Deal.Target), ResourceType = typeof(Resources.Models.Deal))]
        public decimal Target { get; set; }

        [Display(Name = nameof(Deal.Note), ResourceType = typeof(Resources.Models.Deal))]
        public string Note { get; set; }

        [Display(Name = nameof(Deal.State), ResourceType = typeof(Resources.Models.Deal))]
        public DealStates State { get; set; }

        [Display(Name = nameof(Deal.Phase), ResourceType = typeof(Resources.Models.Deal))]
        public DealPhases Phase { get; set; }

        [Display(Name = nameof(Deal.DateClosed), ResourceType = typeof(Resources.Models.Deal))]
        public DateTime DateClosed { get; set; }

        [Display(Name = nameof(Deal.Company), ResourceType = typeof(Resources.Models.Deal))]
        public string Company { get; set; }

        [Display(Name = nameof(Deal.Contact), ResourceType = typeof(Resources.Models.Deal))]
        public string Contact { get; set; }

        [Display(Name = nameof(Deal.IdUser), ResourceType = typeof(Resources.Models.Deal))]
        public string IdUser { get; set; }

        [Display(Name = nameof(Deal.IdUser), ResourceType = typeof(Resources.Models.Deal))]
        public string User { get; set; }

        public int Permits { get; set; }
    }
}
