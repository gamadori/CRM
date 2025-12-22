using CRM.Client.Helpers;
using CRM.Shared;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Services
{
    public class ProjectsService : AGRestClientService, IProjectsService //RestClientService<Company, CompanyFilter, int>, ICompaniesService
    {

        public ProjectsService(HttpClient http) : base(http)
        {
            
        }

        public async Task<bool> AddUser(ProjectUser item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<ProjectUser>($"{ConstHelper.ProjectsPath}/adduser", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveUser(ProjectUser item)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync<ProjectUser>($"{ConstHelper.ProjectsPath}/removeuser", item);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        
    }
}
