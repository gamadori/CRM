using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Helper
{
    public static class CSVHelper
    {
        public static string CSVGetField(string[] row, List<CSVMapping> mappings, string name)
        {
            var map = mappings.Where(x => x.FieldName == name).FirstOrDefault();

            if (map != null && map.NumCol < row.Length)
            {
                return row[map.NumCol];
            }
            else
                return null;
        }
    }
}
