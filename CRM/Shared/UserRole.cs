using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class UserRoles
    {
       
        public string Id { get; set; }

        
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class UserRolesFilter: PagingParameterModel
    {
        public string Role { get; set; }
    }
    public class UserRole
    {
       
        public eRoles IdRole { get; set; }

        public string RoleName { get; set; }

        public bool Selected { get; set; }

    }
}
