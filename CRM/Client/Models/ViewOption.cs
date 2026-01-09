using System;

namespace CRM.Client.Models
{
    public class ViewOption<T> where T: Enum
    {
        public string Text { get; set; }
        public T Value { get; set; }
    }

}
