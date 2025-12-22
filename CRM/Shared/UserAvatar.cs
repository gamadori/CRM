using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class UserAvatar
    {
        [Key]
        public int Id { get; set; }

        public string IdUser { get; set; }
        public string Avatar { get; set; }
    }
}
