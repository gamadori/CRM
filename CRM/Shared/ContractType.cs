using QLNet;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace CRM.Shared
{
   
    public class ContractType
    {
        public int Id { get; set; }

        [Display(Name = nameof(ContractType.Name), ResourceType = typeof(Resources.Models.ContractType))]
        public string Name { get; set; }

        [Display(Name = nameof(ContractType.Description), ResourceType = typeof(Resources.Models.ContractType))]
        public string Description { get; set; }

        [Display(Name = nameof(ContractType.Enabled), ResourceType = typeof(Resources.Models.ContractType))]
        public bool Enabled { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(ContractType.Price), ResourceType = typeof(Resources.Models.ContractType))]
        public decimal Price { get; set; }

        [Display(Name = nameof(ContractType.Discount), ResourceType = typeof(Resources.Models.ContractType))]
        [Column(TypeName = "decimal(16,2)")]
        public decimal Discount { get; set; }

        [Display(Name = nameof(ContractType.Duration), ResourceType = typeof(Resources.Models.ContractType))]
        public int Duration { get; set; }

        [Column(TypeName = "Money")]
        [Display(Name = nameof(ContractType.DiscountedPrice), ResourceType = typeof(Resources.Models.ContractType))]
        public decimal DiscountedPrice { get; set; }

        public ICollection<ContractTypeTicketType> TicketTypes { get; set; }

        [NotMapped]
        public int Permits { get; set; }

    }

    public class ContractTypeFilter : PagingParameterModel
    {
        public bool Enabled { get; set; }
    }
}
