using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.Models
{
    public class BasicImageDropdownItem<T>
    {
        public T? Value { get; set; } // Rimosso il nullable

        public string? Text { get; set; }

        public string? ImageUrl { get; set; }

        public string? Icon { get; set; }
    }
}
