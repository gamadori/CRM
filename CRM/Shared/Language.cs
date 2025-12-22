using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Language
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }    

        public string Description { get; set; } 

        public string LanguageCode { get; set; }    

        public string Flag { get; set; }

        public int Index { get; set; }

        [NotMapped]
        public bool Selected { get; set; }

    }

    public class LanguageFilter : PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string LanguageCode { get; set; }
    }


}
