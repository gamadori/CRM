using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace CRM.Client.Models
{
    public class APIResponseMessage<T>
    {
        public bool State { get; set; }
        public HttpStatusCode Code { get; set; }

        public string Message { get; set; }
        public T Data { get; set; }
    }
}
