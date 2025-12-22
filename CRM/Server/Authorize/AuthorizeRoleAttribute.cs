using CNM.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace CNM.Authorize
{
  

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class AuthorizeRoleAttribute : AuthorizeAttribute
    {
       

        public AuthorizeRoleAttribute(params ePolicy[] roles)
        {
            Policy = string.Join(",", roles.Select(r => r.ToString()));
        }

       
    }
}
