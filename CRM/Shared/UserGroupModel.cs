using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class UserGroupModel
    {
        public int Id { get; set; }

        [Display(Name = nameof(UserGroupModel.IdUser), ResourceType = typeof(Resources.Models.UserGroupModel))]
        [Required(ErrorMessage = "Selezionare l'utente")]
        public string IdUser { get; set; }

        [Display(Name = nameof(UserGroupModel.IdGroup), ResourceType = typeof(Resources.Models.UserGroupModel))]
        [Required]
        public int IdGroup { get; set; }
    }
}
