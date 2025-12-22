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
    public class ArticleAccessory
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(ArticleAccessory.Article))]
        public int IdArticle { get; set; }

        [ForeignKey(nameof(ArticleAccessory.Accessory))]
        public int IdAccessory { get; set; }

        public string Note { get; set; }

        [JsonIgnore]
        public virtual Accessory Accessory { get; set; }


        [JsonIgnore]
        public virtual Article Article { get; set; }
    }

    public class ArticleAccessoryModel
    {
        public int Id { get; set; }

        public int IdArticle { get; set; }

        public int IdAccessory { get; set; }

        public string Note { get; set; }

        public string AccessoryTypeName { get; set; }

        public string AccessoryName { get; set; }

    }
}
