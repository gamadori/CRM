using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class SmtpSetting
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Server { get; set; } = string.Empty;
        [Required]
        public int Port { get; set; } = 25;
        public string SenderName { get; set; } = string.Empty;
        [DataType(DataType.EmailAddress)]
        public string SenderEmail { get; set; } = string.Empty;

        [DataType(DataType.EmailAddress)]
        [Required]
        [Display(Name ="Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [PasswordPropertyText]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool Ssl { get; set; } = false;
    }
}
