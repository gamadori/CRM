using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class IdentityRoleFilter: PagingParameterModel
    {
        public virtual string Name { get; set; }
        
        public virtual string NormalizedName { get; set; }
        
    }
}
