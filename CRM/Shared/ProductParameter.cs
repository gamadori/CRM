using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProductParameter
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int IdProduct { get; set; }
        
        public string Code { get; set; }
        
        public string Name { get; set; }

        public string Description { get; set; }

        public string ValueDefault { get; set; }

        public string Min { get; set; }

        public string Max { get; set; }

        public Product Product { get; set; }

    }

    public class ProductParameterFilter : PagingParameterModel
    {
        public int? IdProduct { get; set; }

        public string? Name { get; set; }
    }
}
