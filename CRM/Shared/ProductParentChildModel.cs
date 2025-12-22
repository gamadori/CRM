using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProductParentChildModel
    {
        public int Id { get; set; }

        [Display(Name = "Sotto Parte")]
        [Required(ErrorMessage = "Selezionare la Sotto Parte")]
        public int IdChild { get; set; }

        [Required]
        public int IdParent { get; set; }
    }
}
