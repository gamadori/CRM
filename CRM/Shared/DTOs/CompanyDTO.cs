using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class CompanyDTO
    {
        public int Id { get; set; }

        [Display(Name = nameof(Company.RagioneSociale), ResourceType = typeof(Resources.Models.Company))]
        public string RagioneSociale { get; set; }
    }
}
