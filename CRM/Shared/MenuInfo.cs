using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class MenuInfo
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("ParentMenu")]
        public int? ParentId { get; set; }

        public string PageName { get; set; }

        public string MenuName { get; set; }

        public string IconName { get; set; }

        public virtual MenuInfo ParentMenu { get; set; }
    }
}
