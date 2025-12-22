using System;
using System.Collections.Generic;
using System.Text;

namespace CRM.Shared
{

    public class LogosFilterModel : PagingParameterModel
    {
        public string Codice { get; set; }

        public string Descrizione { get; set; }
    }
}

