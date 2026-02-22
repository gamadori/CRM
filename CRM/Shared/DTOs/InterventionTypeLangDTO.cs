using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class InterventionTypeLangDTO
    {
        public int Id { get; set; }

        public int IdInterventionType { get; set; }

        public int IdLanguage { get; set; }

        public string Name { get; set; }

        public string NameInterventionType { get; set; } = string.Empty;    

        public string Language { get; set; }

       
    }
}
