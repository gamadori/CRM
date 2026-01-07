
using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;
using System.Net.Http.Json;

namespace CRM.Client.Services
{
    public class ProxyLogosService: ProxyRestClientService<Logo, int, LogosFilterModel, object>, ILogosService
    {

        public ProxyLogosService(HttpClient http) : base(http, ConstHelper.LogosPath)
        {

        }
    }
}
