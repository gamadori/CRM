using CRM.Client.Helpers;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProductParentChildService : IManyToManyService<ProductParentChildModel>
    {
        private readonly HttpClient _http;

        private readonly string _pathService = ConstHelper.ProductParentChild;

        public ProductParentChildService(HttpClient http)
        {
            _http = http;
        }

        public async Task<bool> Post(ProductParentChildModel item)
        {
            
            try
            {
                HttpResponseMessage resp;

               
                resp = await _http.PostAsJsonAsync<ProductParentChildModel>($"{_pathService}", item);
                
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<bool> Delete(ProductParentChildModel item)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();



                param.Add(nameof(item.IdParent), item.IdParent.ToString());
                param.Add(nameof(item.IdChild), item.IdChild.ToString());

                var qs = UriHelper.BuildQueryString(param);



                var resp = await _http.DeleteAsync(_pathService + qs);

                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
