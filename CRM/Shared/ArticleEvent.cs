using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ArticleEvent
    {
        [Key]
        public int Id { get; set; }
        
        [ForeignKey(nameof(Article))]
        public int ArticleId { get; set; }

        [ForeignKey(nameof(Domain))]
        public int DomainId { get; set; }

        [ForeignKey(nameof(EventType))]
        public int EventTypeId { get; set; }

        [ForeignKey(nameof(FromState))]
        public int? FromStateId { get; set; }

        [ForeignKey(nameof(ToState))]   
        public int? ToStateId { get; set; }

        public DateTime OccurredAt { get; set; }
        
        public string? Note { get; set; }
        
        public string? ActorUserId { get; set; }
        
        public int? NewOwnerId { get; set; }

        public virtual Article Article { get; set; }
        
        public virtual ArticleDomain Domain { get; set; }
        
        public virtual ArticleEventType EventType { get; set; }
        
        public virtual ArticleState? FromState { get; set; }

        public virtual ArticleState? ToState { get; set; }

    }
}
