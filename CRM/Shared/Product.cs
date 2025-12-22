using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = nameof(Product.Name), ResourceType = typeof(Resources.Models.Product))]
        public string Name { get; set; }

        [Display(Name = nameof(Product.Description), ResourceType = typeof(Resources.Models.Product))]
        public string Description { get; set; }

        [Display(Name = nameof(Product.Code), ResourceType = typeof(Resources.Models.Product))]
        public string? Code { get; set; }

        [Display(Name = nameof(Product.IdProductType), ResourceType = typeof(Resources.Models.Product))]
        [ForeignKey("ProductType")]
        public int? IdProductType { get; set; }

        public string Property1 { get; set; }

        public string Property2 { get; set; }

        public string Property3 { get; set; }


        [Display(Name = nameof(Product.Price), ResourceType = typeof(Resources.Models.Product))]
        [Column(TypeName = "Money")]
        public decimal Price { get; set; }

        [Display(Name = nameof(Product.ProductType), ResourceType = typeof(Resources.Models.Product))]
        public ProductType ProductType { get; set; }

        [JsonIgnore]
        public ICollection<Article> Articles { get; set; }  
        public ICollection<Product> Parents { get; set; }
        public ICollection<Product> Childs { get; set; }
    }

    public class ProductFilter: PagingParameterModel
    {
        public int? Id { get; set; }
        
        public int? IdParent { get; set; }

        public int? IdCompany { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public string Description { get; set; }
    }

    
}
