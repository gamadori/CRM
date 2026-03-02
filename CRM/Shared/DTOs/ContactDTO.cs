using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class ContactDTO
    {
        public int Id { get; set; }

        [Display(Name = nameof(Contact.Company), ResourceType = typeof(Resources.Models.Contact))]
        public int? IdCompany { get; set; }

        [Display(Name = nameof(Contact.Name), ResourceType = typeof(Resources.Models.Contact))]
        public string Name { get; set; }

        [Display(Name = nameof(Contact.Surname), ResourceType = typeof(Resources.Models.Contact))]
        public string Surname { get; set; }

        [Display(Name = nameof(Contact.Email), ResourceType = typeof(Resources.Models.Contact))]
        public string Email { get; set; }

        [Display(Name = nameof(Contact.Mobile), ResourceType = typeof(Resources.Models.Contact))]
        public string Mobile { get; set; }

        [Display(Name = nameof(Contact.Phone), ResourceType = typeof(Resources.Models.Contact))]
        public string Phone { get; set; }

        [Display(Name = nameof(Contact.Note), ResourceType = typeof(Resources.Models.Contact))]
        public string Note { get; set; }

        // Social links
        [Display(Name = "Facebook", ResourceType = typeof(Resources.Models.Contact))]
        public string? FacebookUrl { get; set; }

        [Display(Name = "LinkedIn", ResourceType = typeof(Resources.Models.Contact))]
        public string? LinkedInUrl { get; set; }

        [Display(Name = "Twitter", ResourceType = typeof(Resources.Models.Contact))]
        public string? TwitterUrl { get; set; }

        [Display(Name = nameof(Contact.NameComplete), ResourceType = typeof(Resources.Models.Contact))]
        public string NameComplete { get { return $"{Surname} {Name}"; } }

        
        public string CompanyName { get; set; }
    }

    public static class ContactHelper
    {
        public static ContactDTO ToDTO(this Contact contact)
        {
            if (contact == null) return null;
            return new ContactDTO
            {
                Id = contact.Id,
                IdCompany = contact.IdCompany,
                Name = contact.Name,
                Surname = contact.Surname,
                Email = contact.Email,
                Mobile = contact.Mobile,
                Phone = contact.Phone,
                Note = contact.Note,
                FacebookUrl = contact.FacebookUrl,
                LinkedInUrl = contact.LinkedInUrl,
                TwitterUrl = contact.TwitterUrl,
                CompanyName = contact.Company != null ? contact.Company.RagioneSociale : string.Empty
            };
        
        }

        public static Contact ToEntity(this ContactDTO dto)
        {
            if (dto == null) return null;
            return new Contact
            {
                Id = dto.Id,
                IdCompany = dto.IdCompany,
                Name = dto.Name,
                Surname = dto.Surname,
                Email = dto.Email,
                Mobile = dto.Mobile,
                Phone = dto.Phone,
                Note = dto.Note,
                FacebookUrl = dto.FacebookUrl,
                LinkedInUrl = dto.LinkedInUrl,
                TwitterUrl = dto.TwitterUrl,    


            };
        }
    }
}
