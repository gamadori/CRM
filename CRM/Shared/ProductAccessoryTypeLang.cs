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
    public class ProductAccessoryTypeLang
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(ProductAccessoryType))]
        public int IdProdAccType { get; set; }

        [ForeignKey(nameof(Language))]
        public int IdLanguage { get; set; }

        public string Name { get; set; }

        [JsonIgnore]

        public virtual ProductAccessoryType ProductAccessoryType { get; set; }

        [JsonIgnore]

        public virtual Language Language { get; set; }

    }

    public class ProductAccessoryTypeLangFilter : PagingParameterModel
    {
        public int? IdProdAccType { get; set; }
    }
}
