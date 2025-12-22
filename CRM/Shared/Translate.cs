using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Translate
    {

        [Key]
        public int Id { get; set; }

        [ForeignKey("Language")]
        public int IdLanguage { get; set; }

        public string Module { get; set; }

        public string Field { get; set; }

        public string Text { get; set; }

        public virtual Language Language { get; set; }
    }
}
