using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public class AverageFeedbackDTO
    {
        public List<AverageFeedbackItemDTO<int>> Companies { get; set; } = new List<AverageFeedbackItemDTO<int>>();
        public List<AverageFeedbackItemDTO<string>> Users { get; set; } = new List<AverageFeedbackItemDTO<string>>();

        /// <summary>
        /// Media complessiva di tutti i feedback
        /// </summary>
        public decimal OverallAverage => Companies.Count > 0 
            ? Math.Round(Companies.Average(c => c.Average), 2) 
            : 0;

        /// <summary>
        /// Totale feedback ricevuti
        /// </summary>
        public int TotalFeedbacks => Companies.Sum(c => c.TotalFeedbacks);
    }

    public class AverageFeedbackItemDTO<K>
    {         
        public K Id { get; set; }

        public string? Name { get; set; }
        
        public decimal Average { get; set; }
        
        public int TotalFeedbacks { get; set; }
    }
}
