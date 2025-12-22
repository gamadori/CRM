using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TicketInterventionArticle
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("TicketIntervention")]
        public int IdTicketIntervention { get; set; }

        [ForeignKey("Product")]
        public int? IdProduct { get; set; }

        [ForeignKey("Article")]
        public int? IdArticle { get; set; }

        public string SerialNumber { get; set; }

        public string Description { get; set; }

        [JsonIgnore]
        public virtual Product Product { get; set; }

        [JsonIgnore]
        public virtual Article Article { get; set; }

        public virtual TicketIntervention TicketIntervention { get; set; }

        
    }

    public class TicketInterventionArticleFilter: PagingParameterModel
    {
        [ForeignKey("Product")]
        public int? IdProdotto { get; set; }

        [ForeignKey("Article")]
        public int? IdArticolo { get; set; }

        public string SerialNumber { get; set; }



    }

    public class TicketInterventionArticleModel
    {
        [Key]
        public Guid Id { get; set; }

        public int IdTicketIntervention { get; set; }

        public int? IdProduct { get; set; }

        public string Product { get; set; }

        public int? IdArticle { get; set; }

        public string Article { get; set; }

        public string SerialNumber { get; set; }

        public string Year { get; set; }
        public string Description { get; set; }

        public int IdLink { get; set; }

    }
}
