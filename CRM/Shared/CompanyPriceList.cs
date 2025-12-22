using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CompanyPriceList
    {
        [Key]
        public int Id { get; set; }

        public int IdCompany { get; set; }

        public int IdProductyType { get; set; }

        [Column(TypeName = "Money")]
        public decimal Price { get; set; }  

    }
}
