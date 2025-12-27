using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class LogEvent
    {
        public enum EventsTypes
        {
            Info,
            Warning,
            Error, 
            Permits
        }

        [Key]
        public int Id { get; set; }

        public DateTime DateEvent { get; set; }

        public string Module { get; set; }

        public string Subroutine { get; set; }    

        public string Message { get; set; }

        public EventsTypes EventType { get; set; }

        public string UserId { get; set; }

        public string User { get; set; }

        public ActivityType? ActivityType { get; set; }
    }
}
