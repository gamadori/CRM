using CRM.Shared;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IWorkflowAutomationClientService : IDataService<WorkflowAutomation, WorkflowAutomation, int, WorkflowAutomationFilter, string>
    {
        Task<PagingResponse<WorkflowAutomationExecutionDTO>?> GetExecutionsAsync(WorkflowAutomationExecutionFilter? filter = null);
        Task<int> RunAsync(int maxItems = 50);
    }
}
