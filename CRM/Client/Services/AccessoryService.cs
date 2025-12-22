using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;

namespace CRM.Client.Services
{
    public class AccessoriesService : RestClientModelService<Accessory, Accessory, AccessoryFilter, int>, IAccessoriesService
    {

        public AccessoriesService(HttpClient http) : base(http, ConstHelper.AccessoriesPath)
        {

        }
        

    }
}
