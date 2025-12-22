using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class BreadcrumbModel
    {
        public string Title { get; set; }   

        public string Description { get; set; } 

        public string Url { get; set; } 

        public Func<object, Task> Action { get; set; }

        public object Data { get; set; }

    }

    public class BreadCrumb<T>
    {
        public T Item { get; set; }

        public List<BreadcrumbModel> Bread { get; set; }
    }
}
