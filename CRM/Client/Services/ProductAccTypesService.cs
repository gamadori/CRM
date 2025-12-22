using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;

namespace CRM.Client.Services
{
    public class ProductAccTypesService : RestClientModelService<ProductAccessoryType, ProductAccessoryTypeModel, ProductAccessoryTypeFilter, int>, IProductAccTypesService
    {

        public ProductAccTypesService(HttpClient http) : base(http, ConstHelper.ProductAccTypesPath)
        {

        }


    }
}
