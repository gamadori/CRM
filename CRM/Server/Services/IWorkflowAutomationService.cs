using CRM.Client.Models;
using CRM.Shared;

namespace CRM.Server.Services
{
    public interface IWorkflowAutomationService
    {
        Task<WorkflowAutomation?> GetItemAsync(int id);
        Task<PagingResponse<WorkflowAutomation>?> GetPagingAsync(WorkflowAutomationFilter? args = null);
        Task<List<WorkflowAutomation>?> GetListAsync(WorkflowAutomationFilter? args = null);
        Task<APIResponseMessage<WorkflowAutomation>> PostAsync(WorkflowAutomation item);
        Task<bool> DeleteAsync(int id);
        Task ExecuteAsync(WorkflowTrigger trigger, Lead? lead = null, Deal? deal = null);
        Task<int> ExecutePendingAsync(int maxItems, CancellationToken cancellationToken = default);
        Task<PagingResponse<WorkflowAutomationExecutionDTO>?> GetExecutionsAsync(WorkflowAutomationExecutionFilter? args = null);
    }
}
