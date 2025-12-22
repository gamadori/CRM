using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;

namespace CRM.Client.Services
{
    public class ProductAccTypesLangService : RestClientService<ProductAccessoryTypeLang, ProductAccessoryTypeLangFilter, int>, IProductAccTypesLangService
    {

        public ProductAccTypesLangService(HttpClient http) : base(http, ConstHelper.ProductAccTypeLangsPath)
        {

        }


    }
}
