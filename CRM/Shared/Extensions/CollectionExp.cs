using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Extensions
{
    public static class CollectionExp
    {
        public static IEnumerable<T> EmptyIfDefault<T>(this IEnumerable<T> collection)
   
        {
            return collection ?? new List<T>();
        }

       
    }
}
