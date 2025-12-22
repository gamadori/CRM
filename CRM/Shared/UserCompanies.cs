using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class UserCompanies
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(User))]
        public string IdUser { get; set; }

        [ForeignKey(nameof(Companies))]
        public int IdCompany { get; set; }

        public virtual ICollection<Company> Companies { get; set; }

        public virtual ApplicationUser User { get; set; }

    }
}
