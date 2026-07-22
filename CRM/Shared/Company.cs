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
    public enum CompanyTypes
    {
        Customer,        
        Reseller,
        HeadCompany
    }


    [Table("Companies")]
    public class Company
    {
        [Key]
        public int Id { get; set; }


        [Display(Name = nameof(Company.RagioneSociale), ResourceType = typeof(Resources.Models.Company))]
        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        public string RagioneSociale { get; set; }

        [Display(Name = nameof(Company.Indirizzo), ResourceType = typeof(Resources.Models.Company))]
        public string? Indirizzo { get; set; }

        [Display(Name = nameof(Company.Cap), ResourceType = typeof(Resources.Models.Company))]
        public string? Cap { get; set; }

        [Display(Name = nameof(Company.Citta), ResourceType = typeof(Resources.Models.Company))]
        public string? Citta { get; set; }

        [Display(Name = nameof(Company.Provincia), ResourceType = typeof(Resources.Models.Company))]
        public string? Provincia { get; set; }

        [Display(Name = nameof(Company.Stato), ResourceType = typeof(Resources.Models.Company))]
        public string? Stato { get; set; }

        [Display(Name = nameof(Company.Email), ResourceType = typeof(Resources.Models.Company))]
        public string? Email { get; set; }

        [Display(Name = nameof(Company.Web), ResourceType = typeof(Resources.Models.Company))]
        public string? Web { get; set; }

        [Display(Name = nameof(Company.Telefono), ResourceType = typeof(Resources.Models.Company))]
        public string? Telefono { get; set; }

        [Display(Name = nameof(Company.Fax), ResourceType = typeof(Resources.Models.Company))]
        public string? Fax { get; set; }

        [Display(Name = nameof(Company.PIva), ResourceType = typeof(Resources.Models.Company))]
        public string? PIva { get; set; }

        [Display(Name = nameof(Company.Mobile), ResourceType = typeof(Resources.Models.Company))]
        public string? Mobile { get; set; }

        [Display(Name = nameof(Company.CodiceFiscale), ResourceType = typeof(Resources.Models.Company))]
        public string? CodiceFiscale { get; set; }

        [Display(Name = nameof(Company.CodiceSDI), ResourceType = typeof(Resources.Models.Company))]
        public string? CodiceSDI { get; set; }

        [Display(Name = nameof(Company.CompanyType), ResourceType = typeof(Resources.Models.Company))]
        public CompanyTypes CompanyType { get; set; }

        [Display(Name = nameof(Company.Note), ResourceType = typeof(Resources.Models.Company))]
        public string Note { get; set; }

        [Display(Name = nameof(Company.IdReseller), ResourceType = typeof(Resources.Models.Company))]
        public int? IdReseller { get; set; }

        [Display(Name = nameof(Company.Logo), ResourceType = typeof(Resources.Models.Company))]
        public string Logo { get; set; }

        public int InternalCode { get; set; }

        [NotMapped]
        public string ResellerName { get; set; }

        [NotMapped]
        public string FormatIndirizzo { get { return $"{this.Indirizzo} - {this.Cap} {this.Citta} - {this.Stato}"; } }
        [JsonIgnore]
        public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; }
    }

    
}
