using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ItemList<T>
    {
        public T Id { get; set; }

        public string Text { get; set; }
    }
}
