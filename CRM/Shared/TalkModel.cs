using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TalkModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(Talk.Name), ResourceType = typeof(Resources.Models.Talk))]
        public string Name { get; set; }

        [Display(Name = nameof(Talk.Date), ResourceType = typeof(Resources.Models.Talk))]
        public DateTime Date { get; set; }

        [Display(Name = nameof(Talk.Company), ResourceType = typeof(Resources.Models.Talk))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Talk.Contact), ResourceType = typeof(Resources.Models.Talk))]
        public int? IdContact { get; set; }

        [Display(Name = nameof(Talk.Amount), ResourceType = typeof(Resources.Models.Talk))]
        public decimal Amount { get; set; }

        [Display(Name = nameof(Talk.Target), ResourceType = typeof(Resources.Models.Talk))]
        public decimal Target { get; set; }

        [Display(Name = nameof(Talk.Note), ResourceType = typeof(Resources.Models.Talk))]
        public string Note { get; set; }

        [Display(Name = nameof(Talk.State), ResourceType = typeof(Resources.Models.Talk))]
        public TalkStates State { get; set; }

        [Display(Name = nameof(Talk.Phase), ResourceType = typeof(Resources.Models.Talk))]
        public TalkPhases Phase { get; set; }

        [Display(Name = nameof(Talk.DateClosed), ResourceType = typeof(Resources.Models.Talk))]
        public DateTime DateClosed { get; set; }

        [Display(Name = nameof(Talk.Company), ResourceType = typeof(Resources.Models.Talk))]
        public string Company { get; set; }

        [Display(Name = nameof(Talk.Contact), ResourceType = typeof(Resources.Models.Talk))]
        public string Contact { get; set; }

        [Display(Name = nameof(Talk.IdUser), ResourceType = typeof(Resources.Models.Talk))]
        public string IdUser { get; set; }

        [Display(Name = nameof(Talk.IdUser), ResourceType = typeof(Resources.Models.Talk))]
        public string User { get; set; }

        public int Permits { get; set; }
    }
}
