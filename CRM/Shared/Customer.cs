using CRM.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    [Table("Customers")]
    public class Customer
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string RagioneSociale { get; set; }
        public string Indirizzo { get; set; }
        public string Cap { get; set; }
        public string Citta { get; set; }
        public string Provincia { get; set; }
        public string Stato { get; set; }
        public string Email { get; set; }
        public string Web { get; set; }
        public string Telefono { get; set; }
        public string PIva { get; set; }

        [ForeignKey("Company")]
        public int? IdCompany { get; set; }

        public virtual Company Company { get; set; }
    }
}
