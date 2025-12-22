using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProductAccessoryType
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }


        [ForeignKey(nameof(AccessoryType))]
        public int IdAccessoryType { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string Name { get; set; }

        public bool Necessary { get; set; } = false;

        public bool Enabled { get; set; } = true;
        

        [NotMapped]
        public int Permits { get; set; }

        public virtual Product Product { get; set; }

        public virtual AccessoryType AccessoryType { get; set; }
    }

    public class ProductAccessoryTypeFilter : PagingParameterModel
    {
        public int? IdProduct { get; set; }
    }

    public class ProductAccessoryTypeModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(ProductAccessoryType.Name), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public string Name { get; set; }

       

        [Display(Name = nameof(ProductAccessoryType.Name), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public string Language { get; set; }

        [Display(Name = nameof(ProductAccessoryType.IdProduct), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public string ProdName { get; set; }
        
        [Display(Name = nameof(ProductAccessoryType.IdAccessoryType), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public string AccTypeName { get; set; }

        [Display(Name = nameof(ProductAccessoryType.Necessary), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public bool Necessary { get; set; }

        [Display(Name = nameof(ProductAccessoryType.Enabled), ResourceType = typeof(Resources.Models.ProductAccessoryType))]
        public bool Enabled { get; set; } = true;
        public int Permits { get; set; }


    }
}
