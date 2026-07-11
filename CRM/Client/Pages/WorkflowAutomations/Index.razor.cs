using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.WorkflowAutomations
{
    public partial class Index : ComponentBase
    {
        [Inject] private IWorkflowAutomationClientService WorkflowService { get; set; } = default!;
        [Inject] private IEnumService EnumService { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;
        private List<WorkflowAutomation> _rules = new();
        private WorkflowAutomation? _selected;
        private bool _loading;
        private bool _saving;
        private string? _message;

        private int ActiveRules => _rules.Count(x => x.IsActive);
        private string LastExecutionText
        {
            get
            {
                var last = _rules
                    .Where(x => x.LastRunAt != null)
                    .Select(x => x.LastRunAt)
                    .OrderByDescending(x => x)
                    .FirstOrDefault();
                return last == null ? "-" : last.Value.ToString("dd/MM HH:mm");
            }
        }

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await LoadRules();
            NewRule();
        }

        private async Task LoadRules()
        {
            _loading = true;
            try
            {
                _rules = await WorkflowService.GetListAsync(new WorkflowAutomationFilter()) ?? new List<WorkflowAutomation>();
            }
            finally
            {
                _loading = false;
            }
        }

        private void NewRule()
        {
            _message = null;
            _selected = new WorkflowAutomation
            {
                IsActive = true,
                Trigger = WorkflowTrigger.LeadCreated,
                ActionType = WorkflowActionType.CreateActivity,
                ActivityKind = ActivityKind.Task,
                ActivitySubject = "Follow-up {LeadName}{DealName}",
                DueDays = 1,
                AssignToOwner = true
            };
        }

        private void EditRule(WorkflowAutomation rule)
        {
            _message = null;
            _selected = new WorkflowAutomation
            {
                Id = rule.Id,
                Name = rule.Name,
                IsActive = rule.IsActive,
                Trigger = rule.Trigger,
                ActionType = rule.ActionType,
                MinAmount = rule.MinAmount,
                LeadSource = rule.LeadSource,
                LeadStatus = rule.LeadStatus,
                DealState = rule.DealState,
                DealPhase = rule.DealPhase,
                ActivityKind = rule.ActivityKind,
                ActivitySubject = rule.ActivitySubject,
                ActivityDescription = rule.ActivityDescription,
                DueDays = rule.DueDays,
                AssignToOwner = rule.AssignToOwner,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt,
                LastRunAt = rule.LastRunAt,
                ExecutionCount = rule.ExecutionCount
            };
        }

        private async Task Save()
        {
            if (_selected == null) return;

            _saving = true;
            _message = null;
            try
            {
                var response = await WorkflowService.PostAsync(_selected);
                if (!response.State)
                {
                    _message = response.Message;
                    return;
                }

                await LoadRules();
                if (response.Data != null)
                {
                    EditRule(response.Data);
                }
            }
            finally
            {
                _saving = false;
            }
        }

        private async Task Delete(int id)
        {
            if (await DialogService.Confirm("Eliminare l'automazione selezionata?", "Elimina") != true)
            {
                return;
            }

            await WorkflowService.DeleteAsync(id);
            await LoadRules();
            NewRule();
        }

        private static string FormatDate(DateTime? value)
            => value == null ? "Mai eseguita" : value.Value.ToString("dd/MM/yyyy HH:mm");

        private string TriggerText(WorkflowTrigger value) => EnumService.Get(typeof(WorkflowTrigger), value);
        private string SourceText(LeadSource value) => EnumService.Get(typeof(LeadSource), value);
        private string LeadStatusText(LeadStatus value) => EnumService.Get(typeof(LeadStatus), value);
        private string DealStateText(DealStates value) => EnumService.Get(typeof(DealStates), value);
        private string DealPhaseText(DealPhases value) => EnumService.Get(typeof(DealPhases), value);
        private string ActivityKindText(ActivityKind value) => EnumService.Get(typeof(ActivityKind), value);
    }
}
