using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class EmailViewModel
    {
        [Required]
        public string To { get; set; } 
        
        public string CC { get; set; }

        public EmailsTypes? emailsType { get; set; } 
        
        public List<string> attachments { get; set; }

        [Required]
        public string Subject { get; set; }

        public string Message { get; set; }
    }
}
