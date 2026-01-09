using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CRM.Shared.LogEvent;

namespace CRM.Shared
{
    

    public class LogEventFilterModel : PagingParameterModel
    {
        public DateTime? DateEvent { get; set; }

        public string? Module { get; set; }

        public string? Subroutine { get; set; }

        public string? Message { get; set; }

        public EventsTypes? EventType { get; set; }
    }
}
