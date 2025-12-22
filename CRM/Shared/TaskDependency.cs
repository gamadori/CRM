using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum TypesDependency
    {
        SS,
        SF,
        FS,
        FF
    }

    public class TaskDependency
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public TypesDependency Type { get; set; }
    }
}
