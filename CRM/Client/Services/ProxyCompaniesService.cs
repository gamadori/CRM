using CRM.Client.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{

    public class ProxyCompaniesService : ProxyRestClientService<Company, CompanyDTO, int, CompanyFilter, object>, ICompaniesService
    {
        public ProxyCompaniesService(HttpClient http) : base(http, ConstHelper.CompaniesPath)
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

        public async Task<IEnumerable<string>> GetEmailAddress(int idCompany)
        {
            try
            {
                var emails = await _http.GetFromJsonAsync<List<string>>($"{ConstHelper.CompaniesPath}/emailaddresses/{idCompany}");
                return emails;
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        public async Task<CompanyDTO?> GetUserCompany()
        {
            try
            {
                var company = await _http.GetFromJsonAsync<CompanyDTO>($"{ConstHelper.CompaniesPath}/user");
                return company;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string> GetLogo(int id)
        {
            var logo = await _http.GetStringAsync($"{ConstHelper.CompaniesPath}/logo/{id}");

            return logo;

        }

        public async Task<List<CompanyTreeNodeDTO>> GetTreeAsync(int? idCompany = null)
        {
            try
            {
                var url = idCompany.HasValue
                    ? $"{ConstHelper.CompaniesPath}/tree?idCompany={idCompany.Value}"
                    : $"{ConstHelper.CompaniesPath}/tree";
                var tree = await _http.GetFromJsonAsync<List<CompanyTreeNodeDTO>>(url);
                return tree ?? new List<CompanyTreeNodeDTO>();
            }
            catch
            {
                return new List<CompanyTreeNodeDTO>();
            }
        }
    }
}
