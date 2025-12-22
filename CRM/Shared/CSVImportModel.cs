using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CSVImportModel
    {
        public Dictionary<string, string> Records { get; set; }

        public List<string> Fields { get; set; }
    }
}
