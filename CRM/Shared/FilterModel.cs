using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class FilterModel<T>
    {
        public bool Enabled { get; set; }

        public T Value { get; set; }
    }
}
