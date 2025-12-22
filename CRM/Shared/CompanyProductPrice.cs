using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CompanyProductPrice
    {
        public int Id { get; set; }

        public int IdCompany { get; set; }

        public int IdProduct { get; set; }

        public decimal? Price { get; set; }

        

    }
}
