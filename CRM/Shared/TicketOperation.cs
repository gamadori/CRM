using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    [Table("TicketOperations")]
    public class TicketwOperation
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("OperationType")]
        public int IdOperationType { get; set; }
        
        public int IdTypeRate { get; set; }

        [DataType("decimal(18,2)")]
        public decimal HourlyRate { get; set; }
        public DateTime DateInit { get; set; }

        public DateTime DateEnd { get; set; }

        public string IdUser { get; set; }

        public string Description { get; set; }

        public virtual OperationType OperationType { get; set; }
    }
}
