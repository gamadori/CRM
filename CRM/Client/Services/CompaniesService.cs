using CRM.Client.Helpers;
using CRM.Shared;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Services
{
    public class CompaniesService : AGRestClientService, ICompaniesService //RestClientService<Company, CompanyFilter, int>, ICompaniesService
    {

        public CompaniesService(HttpClient http) : base(http)
        {
            
        }

        public async Task<bool> AddCustomer(CustomerModel item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<CustomerModel>($"{ConstHelper.CompaniesPath}/addcustomer", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveCustomer(CustomerModel item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<CustomerModel>($"{ConstHelper.CompaniesPath}/removecustomer", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Company?> GetCompany()
        {
            string path;
            try
            {
               
                path = $"{ConstHelper.CompaniesPath}/user";


                var company = await _http.GetFromJsonAsync<Company>(path);

                return company;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                return new Company();
            }
        }
    }
}
