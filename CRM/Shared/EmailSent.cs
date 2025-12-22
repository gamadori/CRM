using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class EmailSent
    {
       
        public int Id { get; set; }

        public DateTime DateSent { get; set; }

        public string IdUser { get; set; }

        public string To { get; set; }

        public string CC { get; set; }

        public string Attchments { get; set; }   

        public string Subject { get; set; }

        public string Message { get; set; }

        public string Result { get; set; }
        public ReportTypes ReportType { get; set; }
    }

    public class EmailSentFilterModel: PagingParameterModel
    {
    }
}
