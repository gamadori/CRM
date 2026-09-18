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

        /// <summary>
        /// Righe totali prima della paginazione. Total e' un aggregato (per i ticket, i minuti
        /// fatturabili), non un conteggio: senza questo campo il controller non ha modo di
        /// sapere quante righe esistono oltre la pagina restituita.
        /// </summary>
        public int TotalCount { get; set; }
    }
}
