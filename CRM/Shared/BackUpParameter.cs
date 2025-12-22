using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class BackUpParameter
    {
        [Key]
        public int Id {  get; set; }

        [ForeignKey(nameof(BackUp))]
        public int IdBackUp { get; set; }

        [ForeignKey(nameof(Parameter))]
        public int? IdParameter { get; set; }

        public string Name { get; set; }
        
        public string Value { get; set; }

        public virtual ProductParameter? Parameter { get; set; }

        public virtual ArticleBackup BackUp { get; set; }


    }
}
