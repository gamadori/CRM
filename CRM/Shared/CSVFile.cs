using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CSVFile
    {
        public string Name { get; set; }

        public string Content { get; set; }

        public string Delimiter { get; set; }


    }

    public class CSVFieldCol
    {
        public string NameField { get; set; }
        public int NumCol { get; set; }
    }
}
