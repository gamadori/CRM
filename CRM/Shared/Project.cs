using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{

    public enum ProjectStates
    { 
        Stop,
        Run,
        Pause,
        Terminated
    }

    public class Project
    {
        [Key]
        public int Id { get; set; }


        [Display(Name = nameof(Name), ResourceType = typeof(Resources.Models.Project))]
        [Required(ErrorMessage = "Il campo {0} è necessario")]
        public string Name { get; set; }

        [Display(Name = nameof(StartDate), ResourceType = typeof(Resources.Models.Project))]
        public DateTime StartDate { get; set; }

        [Display(Name = nameof(EndDate), ResourceType = typeof(Resources.Models.Project))]
        public DateTime EndDate { get; set; }

        [Display(Name = nameof(Description), ResourceType = typeof(Resources.Models.Project))]
        public string Description { get; set; }

        [Display(Name = nameof(State), ResourceType = typeof(Resources.Models.Project))]
        public int State { get; set; }

        [Display(Name = nameof(IdCompany), ResourceType = typeof(Resources.Models.Project))]
        [ForeignKey("Company")]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(IdProduct), ResourceType = typeof(Resources.Models.Project))]
        [ForeignKey("Product")]
        public int? IdProduct { get; set; }

        public int DurationHours { get; set; }

        [Display(Name = nameof(IdUserCreate), ResourceType = typeof(Resources.Models.Project))]
        [ForeignKey("UserCreate")]
        public string IdUserCreate { get; set; }

        [Display(Name = nameof(IdUserLeader), ResourceType = typeof(Resources.Models.Project))]
        [ForeignKey("UserLeader")]
        public string? IdUserLeader { get; set; }

        [NotMapped]
        public int Permits { get; set; } = 0;

        public ICollection<Ticket> Tickets { get; set; }

        public virtual Product? Product { get; set; }

        public virtual Company? Company { get; set; }

        public virtual ApplicationUser UserCreate { get; set; }

        public virtual ApplicationUser UserLeader { get; set; }


    }
    public class ProjectFilter: PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public int? IdProduct { get; set; }
    }

   
}
