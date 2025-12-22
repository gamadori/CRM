using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CompanyContract
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(CompanyContract.IdCompany), ResourceType = typeof(Resources.Models.CompanyContract))]
        [ForeignKey(nameof(Company))]
        public int IdCompany { get; set; }

        [Display(Name = nameof(CompanyContract.IdContractType), ResourceType = typeof(Resources.Models.CompanyContract))]
        [ForeignKey(nameof(ContractType))]
        public int IdContractType { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(CompanyContract.Price), ResourceType = typeof(Resources.Models.CompanyContract))]
        public decimal Price { get; set; }

        [Display(Name = nameof(CompanyContract.DateFrom), ResourceType = typeof(Resources.Models.CompanyContract))]
        public DateTime DateFrom { get; set; }

        [Display(Name = nameof(CompanyContract.DateTo), ResourceType = typeof(Resources.Models.CompanyContract))]
        public DateTime DateTo { get; set; }

        [Display(Name = nameof(CompanyContract.Duration), ResourceType = typeof(Resources.Models.CompanyContract))]
        public int Duration { get; set; }

        [Display(Name = nameof(CompanyContract.Suspended), ResourceType = typeof(Resources.Models.CompanyContract))]
        public bool Suspended { get; set; } = false;

        public bool Enabled { get; set; }


        [NotMapped]
        public int Permits { get; set; }

        [NotMapped]
        public string ContractName { get; set; }

        [NotMapped]
        public bool Active { get; set; }

        [NotMapped]
        public bool Confirm { get; set; } = false;

        [NotMapped]
        public List<ContractTypeTicketTypeModel> TicketTypes { get; set; }

        public virtual Company Company { get; set; }

        public virtual ContractType ContractType { get; set; }

       
        
    }

   

    public class CompanyContractFilter: PagingParameterModel
    {
        public int? IdCompany { get; set; }
        public bool? Active { get; set; } = true;

    }

    public class ContractTicketTypeDetails
    {
        public int IdTicketType { get; set; }

        public int NumUsed { get; set; }

        public int NumAvaible { get; set; }
    }
}
