using CRM.Client.Helpers;
using CRM.Shared;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class ProxyWorkflowAutomationService : ProxyRestClientService<WorkflowAutomation, WorkflowAutomation, int, WorkflowAutomationFilter, string>, IWorkflowAutomationClientService
    {
        public ProxyWorkflowAutomationService(HttpClient http) : base(http, ConstHelper.WorkflowAutomationsPath)
        {
        }

        public async Task<PagingResponse<WorkflowAutomationExecutionDTO>?> GetExecutionsAsync(WorkflowAutomationExecutionFilter? filter = null)
        {
            var qs = CreateQueryString(filter);
            var response = await _http.GetAsync($"{_pathService}/executions{qs}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<PagingResponse<WorkflowAutomationExecutionDTO>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
