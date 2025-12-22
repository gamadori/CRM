using CRM.Shared.Resources.Models;
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
    public class AccessoryTypeLanguage
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(AccessoryType))]
        public int IdAccessoryType { get; set; }

        [ForeignKey(nameof(Language))]
        public int IdLanguage { get; set; }

        [Display(Name = nameof(AccessoryTypeLanguage.Name), ResourceType = typeof(Resources.Models.AccessoryTypeLanguage))]
        public string Name { get; set; }

        //[Display(Name = nameof(AccessoryTypeLanguage.Description), ResourceType = typeof(Resources.Models.AccessoryTypeLanguage))]
        //public string Description { get; set; }

        [Display(Name = nameof(AccessoryTypeLanguage.Name), ResourceType = typeof(Resources.Models.AccessoryTypeLanguage))]
        
        public virtual Language Language { get; set; }

        [JsonIgnore]
        public virtual AccessoryType AccessoryType { get; set; }
       
    }

    public class AccessoryTypeLanguageFilter : PagingParameterModel
    {
        public int? IdAccessoryType { get; set; }
    }
}
