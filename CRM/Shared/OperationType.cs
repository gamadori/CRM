using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    [Table("OperationTypes")]
    public class OperationType
    {
        [Key]
        public int Id { get; set; }

        public string Description { get; set; }
    }
}
