using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ArticleState
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Domain))]
        public int DomainId { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }    

        public string Description { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
       
        public bool IsTerminal { get; set; } 

        public ArticleDomain Domain { get; set; }
    }
}
