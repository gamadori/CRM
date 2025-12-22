using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProductType
    {
        [Key]
        public int Id { get; set; } 
        
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        public bool Property1 { get; set; }

        public bool Property2 { get; set; }

        public bool Property3 { get; set; }



    }

    public class ProductTypeFilter : PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }


    }
}
