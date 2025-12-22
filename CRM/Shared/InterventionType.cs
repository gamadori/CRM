using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class InterventionType
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Name { get; set; }

        public string Description { get; set; }

        [JsonIgnore]
        public virtual ICollection<TicketIntervention> TicketsInterventionType { get; set; }

        
        public virtual ICollection<InterventionTypeLanguage> InterventionTypeLanguages { get; set; }
    }

    public class InterventionTypeFilter: PagingParameterModel
    {
        public string Name { get; set; }

        public string Description { get; set; }
    }

}
