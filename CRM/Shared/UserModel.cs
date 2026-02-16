using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    
    public class UserModel
    {
        public string Id { get; set; }


        [Display(Name = nameof(ApplicationUser.IdCompany), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public int? IdCompany { get; set; }

        
        public int? IdCustomer { get; set; }

        [Display(Name = nameof(ApplicationUser.Name), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [Required(ErrorMessage = "Il campo {0} è necessario.")]
        public string Name { get; set; }

        [Display(Name = nameof(ApplicationUser.Surname), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [Required(ErrorMessage ="Il campo {0} è necessario.")]        
        public string Surname { get; set; }

        [Display(Name = nameof(ApplicationUser.Email), ResourceType = typeof(Resources.Models.ApplicationUser))]
        [Required(ErrorMessage ="Il campo {0} è necessario")]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = nameof(ApplicationUser.PhoneNumber), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string PhoneNumber { get; set; }

        [Display(Name = nameof(ApplicationUser.LanguageCode), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string LanguageCode { get; set; }

        [Display(Name = nameof(ApplicationUser.IdCompany), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Company { get; set; }

        public string UserName { get; set; }

        [Display(Name = nameof(ApplicationUser.Color), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Color { get; set; }

        [Display(Name = nameof(ApplicationUser.Photo), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string Photo { get; set; }

        [Display(Name = nameof(ApplicationUser.Enabled), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool Enabled { get; set; }

        [Display(Name = nameof(ApplicationUser.AdminConfirmed), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public bool AdminConfirmed { get; set; }

        public string AvatarTxt { get; set; }

        [Display(Name = nameof(ApplicationUser.CompanyPreview), ResourceType = typeof(Resources.Models.ApplicationUser))]
        public string CompanyPreview { get; set; }

        public bool IsDeleted { get; set; }

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

    public static class UserModelExtension
    {
        public static UserModel ToUserModel(this ApplicationUser user)
        {
            if (user == null)
                return null;
            return new UserModel
            {
                Id = user.Id,
                IdCompany = user.IdCompany,
               
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                LanguageCode = user.LanguageCode,
                Company = user.Company != null ? user.Company.RagioneSociale : null,
                UserName = user.UserName,
                Color = user.Color,
                Photo = user.Photo,
                Enabled = user.Enabled,
                AdminConfirmed = user.AdminConfirmed,
                AvatarTxt = $"{user.Name.Substring(0, 1)}{user.Surname.Substring(0, 1)}",
               
            };
        }
    }

    //public class LoginModel
    //{
    //    public string UserName { get; set; }

    //    public string Password { get; set; }    

    //    public string Token { get; set; }   
    //}
}
