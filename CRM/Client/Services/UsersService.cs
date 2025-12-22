using CRM.Client.Helpers;
using CRM.Shared;

using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class UsersService: AGRestClientService, IUserService //RestClientService<ApplicationUser, UsersFilterModel, string>, IUserService
    {
        private string _pathService = ConstHelper.UsersPath;
        public UsersService(HttpClient http) : base(http)
        {

        }
       


        public async Task<ApplicationUser> Confirm(string id)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApplicationUser>($"{_pathService}/Confirm/{id}");

                return resp;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<bool> SendInvite(string id)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<bool>($"{_pathService}/SendInvite/{id}");
                return resp;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);

                return false;
            }
        }

        public async Task<ApplicationUser> CurrentUser()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<ApplicationUser>($"{_pathService}/CurrentUser");
                return resp;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public async Task<bool> CheckPolicy(ePolicy policy)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<bool>($"{_pathService}/checkpolicy/{(int)policy}");
                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        public async Task<CompanyTypes> GetCompanyType()
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<int>($"{_pathService}/companytype");
                return (CompanyTypes)resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return  CompanyTypes.Customer;
            }
        }

        public async Task<bool> Disable(string id)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<bool>($"{_pathService}/disable/{id}");

                return resp;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
