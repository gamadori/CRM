using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class SmtpSettings
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Server { get; set; }
        [Required]
        public int Port { get; set; } 
        public string SenderName { get; set; }
        [DataType(DataType.EmailAddress)]
        public string SenderEmail { get; set; }

        [DataType(DataType.EmailAddress)]
        [Required]
        [Display(Name ="Username")]
        public string Username { get; set; }

        [Required]
        [PasswordPropertyText]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool Ssl { get; set; }
    }
}
