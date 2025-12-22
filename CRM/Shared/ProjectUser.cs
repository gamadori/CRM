using CRM.Shared.Resources.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ProjectUser
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Project))]
        public int IdProject { get; set; }

        [ForeignKey(nameof(User))]
        public string IdUser { get; set; }

        public virtual Project Project { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}
