using CRM.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CRM.Server.Helpers
{
    public class RolesHelper
    {
        public static async Task CreateUserRoles(IServiceProvider serviceProvider)
        {
            var RoleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var UserManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            IdentityResult roleResult;

            foreach (eRoles role in Enum.GetValues(typeof(eRoles)))
            {
                var r = role.ToString();
                var roleCheck = await RoleManager.RoleExistsAsync(r);
                if (!roleCheck)
                {
                    //create the roles and seed them to the database
                    roleResult = await RoleManager.CreateAsync(new IdentityRole(r));
                }
            }


        }
    }
}
