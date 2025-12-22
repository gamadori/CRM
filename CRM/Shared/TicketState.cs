using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public enum eTicketStates
    {
        Created,
        Assigned,
        Processing,
        Expired,
        Closed

    }

    [Table("TicketStates")]
    public class TicketState
    {
        [Key]
        public int Id { get; set; }

        public int State { get; set; }

        public string Description { get; set; }

        public string Color { get; set; }
        [NotMapped]
        public eTicketStates? idState { get; set; }
    }

    public class TicketStateFilter: PagingParameterModel
    {
        public string Description { get; set; }
    }

   
}


