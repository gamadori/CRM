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
    public class Contact
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = nameof(Contact.Company), ResourceType = typeof(Resources.Models.Contact))]
        [ForeignKey("Company")]
        public int? IdCompany { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
        [Display(Name = nameof(Contact.Name), ResourceType = typeof(Resources.Models.Contact))]
        public string Name { get; set; }

        [Required(ErrorMessageResourceName = "Required", ErrorMessageResourceType = typeof(Resources.ErrorMessages.AppErrorMessage))]
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
        [NotMapped]
        public string NameComplete { get { return $"{Surname} {Name}"; } }
        public virtual Company? Company { get; set; }

        [JsonIgnore]
        public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; } = new List<ApplicationUser>();
    }

    public class ContactFilter: PagingParameterModel
    {
        public int? IdCompany { get; set; }

        public string? Name { get; set; }
    }
}
