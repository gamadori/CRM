using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ArticleStateTransition
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Domain))]
        public int DomainId { get; set; }

        [ForeignKey(nameof(FromState))]
        public int FromStateId { get; set; }

        [ForeignKey(nameof(EventType))]
        public int EventTypeId { get; set; }

        [ForeignKey(nameof(ToState))]
        public int ToStateId { get; set; }

        public ArticleDomain Domain { get; set; }

        public ArticleState FromState { get; set; }

        public ArticleEventType EventType { get; set; }

        public ArticleState ToState { get; set; }
    }
}
