using System;
using System.Collections.Generic;
using System.Text;

namespace CRM.Shared
{

    public class CompanyFilter : PagingParameterModel
    {
        public int Id { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string RagioneSociale { get; set; }

        public string Stato { get; set; }

        public int? IdReseller { get; set; }

        /// <summary>
        /// Filtro usato nella creazione dell'elenco delle ditte che devono essere assegnate a un rivenditore
        /// per non inserire quelle gia assegnate.
        /// </summary>
        public int? IdCompanyParent { get; set; }

        public CompanyTypes? CompanyType { get; set; } = null;

        /// <summary>
        /// Filtro per selezionare l'elenco dei rivenditori
        /// </summary>
        public bool Reseller { get; set;} = false;

    }
}

