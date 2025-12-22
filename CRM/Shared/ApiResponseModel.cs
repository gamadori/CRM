using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class ApiResponseModel
    {
        public bool State { get; set; }
        public string Message { get; set; }

        public object Item { get; set; }

        public int Code { get; set; }

    }
}
