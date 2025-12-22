using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProductCompanyPrice
    {
        
        public int Id { get; set; }

        [ForeignKey("ProductType")]
        public int IdProduct { get; set; }

        [ForeignKey("Company")]
        public int IdCompany { get; set; }

        [Column(TypeName = "Money")]
        public decimal Price { get; set; }

        public Product ProductType { get; set; }

        public Company Company { get; set; }
    }
}
