using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Extensions
{
    public static class StringExp
    {
        public static List<string> ToList(this string s, string sep = null)
        {
            
            sep = sep ?? ";";

            return s.Split(sep).ToList();
        }

        public static string FromList(this List<string>list, string sep = null)
        {
            string value = "";
            sep = sep ?? ";";

            foreach (var item in list)
            {
                if (value.Length > 0)
                    value += sep;

                value += item;
            }
            return value;
        }
    }
}
