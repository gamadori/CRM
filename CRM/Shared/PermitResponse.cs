using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class PermitResponse
    {
        public bool CanAccess { get; set; }

        public int? IdCompany { get; set; }

        public List<int> IdCompanies { get; set; }
    }
}
