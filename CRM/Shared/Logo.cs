using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    [Table("Loghi")]
    public class Logo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Codice { get; set; }

        public string Descrizione { get; set; }

        public string  InputFile { get; set; }
    }
}
