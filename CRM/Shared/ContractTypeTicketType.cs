using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ContractTypeTicketType
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(ContractTypeTicketType.IdContractType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        [ForeignKey(nameof(ContractType))]
        public int IdContractType { get; set; }

        [Display(Name = nameof(ContractTypeTicketType.IdTicketType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        [ForeignKey(nameof(TicketType))]
        public int IdTicketType { get; set; }

        [Display(Name = nameof(ContractTypeTicketType.Unlimited), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public bool Unlimited { get; set; } = false;

        [Display(Name = nameof(ContractTypeTicketType.NumIntervention), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public int NumIntervention { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(ContractTypeTicketType.Price), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public decimal Price { get; set; }

        
       

        public ContractType ContractType { get; set; }

        public TicketType TicketType { get; set; }

    }

    public class ContractTypeTicketTypeModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(ContractTypeTicketType.IdContractType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public int IdContractType { get; set;}

        [Display(Name = nameof(ContractTypeTicketType.IdTicketType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public int IdTicketType { get; set;}

        [Display(Name = nameof(ContractTypeTicketType.Unlimited), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public bool Unlimited { get; set; } = false;

        [Display(Name = nameof(ContractTypeTicketType.NumIntervention), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public int NumIntervention { get; set; }

       
        [Display(Name = nameof(ContractTypeTicketType.IdContractType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public string ContractTypeName { get; set; }

        [Display(Name = nameof(ContractTypeTicketType.IdTicketType), ResourceType = typeof(Resources.Models.ContractTypeTicketType))]
        public string TicketTypeName { get; set; }

        public int NumIntExecuted { get; set; }

        public int Permits { get; set; }

    }
    public class ContractTypeTicketTypeFilter: PagingParameterModel
    {
        public int? IdContractType { get; set; }

        public int? IdTicketType { get; set; }

    }
}
