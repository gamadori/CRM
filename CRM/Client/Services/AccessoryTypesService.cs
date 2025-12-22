using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;

namespace CRM.Client.Services
{
    public class AccessoryTypesService : RestClientModelService<AccessoryType,  AccessoryTypeModel, AccessoryTypeFilter, int>, IAccessoryTypesService
    {

        public AccessoryTypesService(HttpClient http) : base(http, ConstHelper.AccessoryTypesPath)
        {

        }


    }
}
