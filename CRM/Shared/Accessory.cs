using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Accessory
    {
        [Key]
        public int Id { get; set; }

        
        [Display(Name = nameof(Accessory.IdAccessoryType), ResourceType = typeof(Resources.Models.Accessory))]
        [ForeignKey("AccessoryType")]
        public int IdAccessoryType { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = nameof(Accessory.Name), ResourceType = typeof(Resources.Models.Accessory))]
        public string Name { get; set; }

        [Display(Name = nameof(Accessory.SupplierCode), ResourceType = typeof(Resources.Models.Accessory))]
        public string SupplierCode { get; set; }

        [Display(Name = nameof(Accessory.Code), ResourceType = typeof(Resources.Models.Accessory))]
        public string Code { get; set; }

        [Display(Name = nameof(Accessory.Description), ResourceType = typeof(Resources.Models.Accessory))]
        public string Description { get; set; }

        public DateTime Date { get; set; }

        public string IdUser { get; set; }

        [NotMapped]
        public int Permits { get; set; }

        [Display(Name = nameof(Accessory.AccessoryType), ResourceType = typeof(Resources.Models.Accessory))]
        public virtual AccessoryType AccessoryType { get; set; }

        [NotMapped]
        public string Type { get; set; }
    }

    public class AccessoryFilter : PagingParameterModel
    {
        public int? IdAccessoryType { get; set; }
    }

}
