using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ObjectView<M, T>
    {
       
        public List<M> Items { get; set; } = new List<M>();

        public T? Total { get; set; }

        /// <summary>Metadati di paginazione (conteggio totale, ecc.). Il conteggio totale
        /// va calcolato prima di Skip/Take, non dedotto dagli Items della pagina corrente.</summary>
        public PagingHeaderModel? MetaData { get; set; }


    }
}
