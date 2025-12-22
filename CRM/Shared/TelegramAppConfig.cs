using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum TelegramButState
    {
        Connecting,
        Connected,
        ConfigNeed
    }

    public class TelegramStatus
    {
        public TelegramButState State { get; set; }

        public string User { get; set; }

        public string Desc { get; set; }
    }
    public class TelegramAppConfig
    {
        [Key]
        public int Id { get; set; }
        public string AppApi_id { get; set; }

        public string AppApi_hash { get; set; }

        public string MobileNumber { get; set; }


    }
}
