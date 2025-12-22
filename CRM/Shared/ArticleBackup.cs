using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ArticleBackup
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Article))]
        public int IdArticle { get; set; }

        public DateTime TimeStamp { get; set; }

        public string Description { get; set; }

        public  Article Article { get; set; }
    }

    public class ArticleBackupFilter : PagingParameterModel
    {

        public int? IdArticle { get; set; }

        public DateTime? TimeStampFrom { get; set; }

        public DateTime? TimeStampTo { get; set; }
    }
}
