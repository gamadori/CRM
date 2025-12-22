using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class InterventionTypeLanguage
    {
        public int Id { get; set; }

        public int IdInterventionType { get; set; }

        [ForeignKey("Language")]
        public int IdLanguage { get; set; }

        public string Name { get; set; }


        public virtual Language Language { get; set; }
    }

    public class InterventionTypeLang
    {
        public int Id { get; set; }

       
        public int IdLanguage { get; set; }

        public string Language { get; set; }

        public string Name { get; set; }

    }

    public class InterventionTypeLangFilter: PagingParameterModel
    {

        public int? IdInterventionType { get; set; }

        public int? IdLanguage { get; set; }
    }
}
