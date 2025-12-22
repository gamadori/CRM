using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum eRoles
    {
        [Description("Admin")]
        Admin,

        [Description("Super User")]
        SuperUser,

        [Description("Standard")]
        Standard,

        [Description("Client")]
        Client

    }

    public enum ePolicy
    {
        [Description("Admin Role")]
        AdminRole,

        [Description("Super User Role")]
        SuperUserRole,

        [Description("Standard Role")]
        StandardRole,

        [Description("Client Role")]
        ClientRole
    }

    public class PolicyRoles
    {
        public static List<string>[] vPoliyRoles = new List<string>[] 
        { 
            new List<string>(){ eRoles.Admin.ToString() }, 
            new List<string>() {eRoles.Admin.ToString(), eRoles.SuperUser.ToString() },
            new List<string>() {eRoles.Admin.ToString(), eRoles.SuperUser.ToString(), eRoles.Standard.ToString() },
            new List<string>() {eRoles.Admin.ToString(), eRoles.SuperUser.ToString(), eRoles.Standard.ToString(), eRoles.Client.ToString()},
        };
    }
}
