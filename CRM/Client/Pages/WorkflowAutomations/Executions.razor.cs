using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.WorkflowAutomations
{
    public partial class Executions : ComponentBase
    {
        [Inject] private IWorkflowAutomationClientService WorkflowService { get; set; } = default!;
        [Inject] private IEnumService EnumService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;
        private List<WorkflowAutomation> _rules = new();
        private List<WorkflowAutomationExecutionDTO> _executions = new();
        private WorkflowAutomationExecutionFilter _executionFilter = new() { PageSize = 25, Skip = 0, Top = 25 };
        private bool _executionsLoading;
        private string? _executionStatus;
        private int _failedExecutions;
        private int _totalExecutionCount;

        private int TotalExecutions => _totalExecutionCount > 0 ? _totalExecutionCount : _executions.Count;
        private string LastExecutionText
        {
            get
            {
                var last = _executions
                    .OrderByDescending(x => x.ExecutedAt)
                    .FirstOrDefault()
                    ?.ExecutedAt;

                return last == null ? "-" : last.Value.ToString("dd/MM HH:mm");
            }
        }

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadRules();
            await LoadExecutions();
        }

        private async Task LoadRules()
        {
            _rules = await WorkflowService.GetListAsync(new WorkflowAutomationFilter()) ?? new List<WorkflowAutomation>();
        }

        private async Task LoadExecutions()
        {
            _executionsLoading = true;
            try
            {
                _executionFilter.Skip = 0;
                _executionFilter.Top = _executionFilter.Top == 0 ? 25 : _executionFilter.Top;
                var response = await WorkflowService.GetExecutionsAsync(_executionFilter);
                _executions = response?.Items ?? new List<WorkflowAutomationExecutionDTO>();
                _totalExecutionCount = response?.MetaData?.TotalCount ?? _executions.Count;
                _failedExecutions = _executions.Count(x => !x.Succeeded);
            }
            finally
            {
                _executionsLoading = false;
            }
        }

        private async Task OnExecutionStatusChanged()
        {
            _executionFilter.Succeeded = _executionStatus switch
            {
                "ok" => true,
                "error" => false,
                _ => null
            };

            await LoadExecutions();
        }

        private string TriggerText(WorkflowTrigger value) => EnumService.Get(typeof(WorkflowTrigger), value);
        private string ActivityKindText(ActivityKind value) => EnumService.Get(typeof(ActivityKind), value);
        private string EntityText(ActivityEntityType value) => EnumService.Get(typeof(ActivityEntityType), value);
    }
}
