using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class Test
    {
        [Key]
        public int Id { get; set; }

        string Name { get; set; }
    }

    public class Test1: Test
    {
        public string Description { get; set; }
    }

    public class Test2: Test
    {
        public string Prova { get; set; }
    }
}
