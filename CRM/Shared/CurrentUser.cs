using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CurrentUser
    {
        public bool IsAuhenticated { get; set; }
        public string UserName { get; set; }

        public string IdUser { get; set; }
    }
}
