using CRM.Shared.Resources.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class AccessoryType
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = nameof(AccessoryType.Name), ResourceType = typeof(Resources.Models.AccessoryType))]
        public string Name { get; set; }


        [Display(Name = nameof(AccessoryType.Description), ResourceType = typeof(Resources.Models.AccessoryType))]
        public string? Description { get; set; }

        public DateTime Date { get; set; }

        public string IdUser { get; set; }

        [NotMapped]
        public string Language { get; set; }

        [NotMapped]
        public int Permits { get; set; }

        public virtual ICollection<AccessoryTypeLanguage> Languages { get; set; }
    }

    public class AccessoryTypeFilter : PagingParameterModel
    {
        public bool Translate { get; set; } = false;
    }

    public class AccessoryTypeModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(AccessoryType.Name), ResourceType = typeof(Resources.Models.AccessoryType))]
        public string Name { get; set; }

        [Display(Name = nameof(AccessoryType.Description), ResourceType = typeof(Resources.Models.AccessoryType))]
        public string Description { get; set; }

        [Display(Name = nameof(AccessoryType.Name), ResourceType = typeof(Resources.Models.AccessoryType))]
        public string Language { get; set; }

        public int Permits { get; set; }

        
    }
}
