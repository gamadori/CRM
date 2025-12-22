
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketTypeLanguage
    {
        public int Id { get; set; }

        [ForeignKey("TicketType")]
        public int IdTicketType { get; set; }

        [ForeignKey("Language")]
        public int IdLanguage { get; set; }

        public string Name { get; set; }

        public virtual Language Language { get; set; }

        [JsonIgnore]
        public virtual TicketType TicketType { get; set; }
    }

   

    public class TicketTypeLanguageFilter : PagingParameterModel
    {
        public int? IdTicketType { get; set; }

        public int? IdLanguage { get; set; }
    }
}
