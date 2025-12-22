using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProjectModel
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Nome")]
        [Required(ErrorMessage = "Il campo {0} è necessario")]
        public string Name { get; set; }

        [Display(Name = "Data Inizio")]
        public DateTime StartDate { get; set; }

        [Display(Name = "Data Fine")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Descrizione")]
        public string Description { get; set; }

        public int DurationHours { get; set; }

        public int? ProductType { get; set; }
        public int IdUser { get; set; }
    }

    public class ProjectModelFilter: PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }
    }

   
}
