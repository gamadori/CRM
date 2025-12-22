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
    public class GroupUsersService: IManyToManyService<UserGroupModel>
    {
        private readonly HttpClient _http;

        private readonly string _pathService = ConstHelper.GroupUsers;

        public GroupUsersService(HttpClient http)
        {
            _http = http;
        }

        public async Task<bool> Post(UserGroupModel item)
        {
            
            try
            {
                HttpResponseMessage resp;

               
                resp = await _http.PostAsJsonAsync<UserGroupModel>($"{_pathService}", item);
                
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<bool> Delete(UserGroupModel item)
        {
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();



                param.Add(nameof(item.IdGroup), item.IdGroup.ToString());
                param.Add(nameof(item.IdUser), item.IdUser);

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
