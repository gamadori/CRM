using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class CSVMapping
    {
        [Key]
        public int Id { get; set; }

        public string TableName { get; set; }

        public string FieldName { get; set; }

        public int NumCol { get; set; }

    }
}
