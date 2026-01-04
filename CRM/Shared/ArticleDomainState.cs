using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ArticleDomainState
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Article")]
        public int ArticleId { get; set; }
        [ForeignKey("Domain")]
        public int DomainId { get; set; }
        [ForeignKey("CurrentState")]
        public int CurrentStateId { get; set; }
        [ForeignKey("LastEvent")]
        public int? LastEventId { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        [Column(TypeName = "rowversion")]
        public byte[] RowVer { get; set; } = Array.Empty<byte>();

        public virtual ArticleDomain Domain { get; set; }

        public virtual ArticleState CurrentState { get; set; }

        public virtual ArticleEvent? LastEvent { get; set; }

        public virtual Article Article { get; set; }
    }
}
