using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

        [Display(Name = nameof(Company.Master), ResourceType = typeof(Resources.Models.Company))]
        public bool Master { get; set; }
        
        [Display(Name = nameof(Company.InternalCode), ResourceType = typeof(Resources.Models.Company))] 
        public int InternalCode { get; set; }
        
        [Display(Name = nameof(Company.ResellerName), ResourceType = typeof(Resources.Models.Company))]
        public string ResellerName { get; set; }

        
    }

    public static class CompanyHelper
    {
        public static CompanyDTO ToDTO(this Company company)
        {
            if (company == null) return null;
            return new CompanyDTO   
            {
               Id = company.Id,
               RagioneSociale = company.RagioneSociale,
                Cap = company.Cap,
                Citta = company.Citta,
                CodiceFiscale = company.CodiceFiscale,
                CodiceSDI = company.CodiceSDI,
                CompanyType = company.CompanyType,
                Email = company.Email,
                IdReseller = company.IdReseller,
                Indirizzo = company.Indirizzo,
                Master = company.Master,
                Mobile = company.Mobile,
                Note = company.Note,
                PIva = company.PIva,
                Provincia = company.Provincia,
                ResellerName = company.ResellerName,
                Telefono = company.Telefono,
                Web = company.Web,
                Fax = company.Fax,
                InternalCode = company.InternalCode

            };
        }

        public static Company ToEntity(this CompanyDTO dto)
        {
            if (dto == null) return null;
            return new Company
            {
                Id = dto.Id,
                RagioneSociale = dto.RagioneSociale,
                Cap = dto.Cap,
                Citta = dto.Citta,
                CodiceFiscale = dto.CodiceFiscale,
                CodiceSDI = dto.CodiceSDI,
                CompanyType = dto.CompanyType,
                Email = dto.Email,
                IdReseller = dto.IdReseller,
                Indirizzo = dto.Indirizzo,
                Master = dto.Master,
                Mobile = dto.Mobile,
                Note = dto.Note,
                PIva = dto.PIva,
                Provincia = dto.Provincia,
                Telefono = dto.Telefono,
                Web = dto.Web,
                Fax = dto.Fax,
                InternalCode = dto.InternalCode,

            };
        }
    }
}
