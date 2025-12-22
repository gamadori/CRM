using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    
    public class UserContactModel
    {
        public string Id { get; set; }


        [Display(Name = nameof(ApplicationUser.IdCompany), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public int? IdCompany { get; set; }

       

        [Display(Name = nameof(ApplicationUser.Name), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [Required(ErrorMessage = "Il campo {0} è necessario.")]
        public string Name { get; set; }

        [Display(Name = nameof(ApplicationUser.Surname), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [Required(ErrorMessage ="Il campo {0} è necessario.")]        
        public string Surname { get; set; }

        [Display(Name = nameof(ApplicationUser.Email), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = nameof(ApplicationUser.PhoneNumber), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string PhoneNumber { get; set; }

        


        [Display(Name = nameof(ApplicationUser.IdCompany), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string? Company { get; set; }

        public string UserName { get; set; }

        

        [Display(Name = nameof(ApplicationUser.Photo), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Photo { get; set; }

      

        [Display(Name = nameof(ApplicationUser.CompanyPreview), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string CompanyPreview { get; set; }


        [Display(Name = nameof(ApplicationUser.NameComplete), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string NameComplete
        {

            get
            {
                if (this != null)
                    return $"{this?.Surname} {this?.Name}";
                else
                    return null;
            }
        }

    }

    public class UserContactModelFilter : PagingParameterModel
    {
        public int? IdCompany { get; set; }

        public string? Name { get; set; }
    }


}
