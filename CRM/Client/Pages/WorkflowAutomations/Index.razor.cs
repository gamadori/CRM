using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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
        [Inject] private IBaseRestService<ApplicationUser, UsersFilterModel, string> UsersService { get; set; } = default!;
        [Inject] private IInitiativeService InitiativeService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;
        private List<WorkflowAutomation> _rules = new();
        private List<ApplicationUser> _users = new();
        private List<InitiativeDTO> _initiatives = new();
        private WorkflowAutomation? _selected;
        private bool _loading;
        private bool _saving;
        private bool _running;
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

        private bool IsLeadRule => _selected != null && IsLeadTrigger(_selected.Trigger);
        private bool IsDealRule => _selected != null && IsDealTrigger(_selected.Trigger);
        private LeadStatus? ImpliedLeadStatus => _selected == null ? null : ImpliedLeadStatusFor(_selected.Trigger);
        private DealStates? ImpliedDealState => _selected == null ? null : ImpliedDealStateFor(_selected.Trigger);

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await Task.WhenAll(LoadRules(), LoadUsers(), LoadInitiatives());
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

        private async Task LoadUsers()
        {
            var response = await UsersService.Get(new UsersFilterModel { PageSize = 0 });
            _users = response?.Items?
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.NameComplete)
                .ToList() ?? new List<ApplicationUser>();
        }

        private async Task LoadInitiatives()
        {
            // Le piu' recenti in cima: una regola si scrive per la fiera che sta per aprire o che
            // si e' appena chiusa, non per quella di tre anni fa.
            _initiatives = (await InitiativeService.GetListAsync(new InitiativeFilter()))?
                .OrderByDescending(x => x.DateFrom)
                .ToList() ?? new List<InitiativeDTO>();
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
                AssignToOwner = true,
                IdAssignee = null
            };
            NormalizeSelectedRule();
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
                IdInitiative = rule.IdInitiative,
                DealState = rule.DealState,
                DealPhase = rule.DealPhase,
                ActivityKind = rule.ActivityKind,
                ActivitySubject = rule.ActivitySubject,
                ActivityDescription = rule.ActivityDescription,
                DueDays = rule.DueDays,
                AssignToOwner = rule.AssignToOwner,
                IdAssignee = rule.IdAssignee,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt,
                LastRunAt = rule.LastRunAt,
                ExecutionCount = rule.ExecutionCount
            };
            NormalizeSelectedRule();
        }

        private void OnTriggerChanged()
        {
            NormalizeSelectedRule();
        }

        private async Task Save()
        {
            if (_selected == null) return;

            _saving = true;
            _message = null;
            try
            {
                NormalizeSelectedRule();
                if (!ValidateSelectedRule())
                {
                    return;
                }

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

        private async Task RunNow()
        {
            _running = true;
            _message = null;
            try
            {
                var executed = await WorkflowService.RunAsync();
                _message = executed == 0
                    ? "Nessuna automazione da eseguire."
                    : $"Automazioni eseguite: {executed}.";
                await LoadRules();
            }
            finally
            {
                _running = false;
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
        private static string InitiativeLabel(InitiativeDTO item)
            => $"{Initiatives.Index.KindText(item.Kind)} - {item.Name} ({item.DateFrom:dd/MM/yyyy})";

        /// <summary>
        /// Il nome dell'iniziativa a cui una regola e' legata, per l'elenco. Se il vincolo c'e' ma
        /// l'iniziativa non e' fra quelle caricate si dice che c'e' comunque: tacere farebbe leggere
        /// la regola come se valesse per tutti.
        /// </summary>
        private string? RuleInitiativeText(WorkflowAutomation rule)
        {
            if (rule.IdInitiative == null)
            {
                return null;
            }

            var initiative = _initiatives.FirstOrDefault(x => x.Id == rule.IdInitiative);
            return initiative == null ? $"Iniziativa #{rule.IdInitiative}" : initiative.Name;
        }

        private bool ValidateSelectedRule()
        {
            if (_selected == null) return false;

            if (!_selected.AssignToOwner && string.IsNullOrWhiteSpace(_selected.IdAssignee))
            {
                _message = "Seleziona l'utente a cui assegnare l'attivita oppure scegli l'owner del record.";
                return false;
            }

            return true;
        }

        private void NormalizeSelectedRule()
        {
            if (_selected == null) return;

            if (_selected.AssignToOwner)
            {
                _selected.IdAssignee = null;
            }

            if (IsLeadTrigger(_selected.Trigger))
            {
                _selected.LeadStatus = ImpliedLeadStatusFor(_selected.Trigger);
                _selected.DealState = null;
                _selected.DealPhase = null;
                return;
            }

            _selected.LeadSource = null;
            _selected.LeadStatus = null;
            _selected.DealState = ImpliedDealStateFor(_selected.Trigger);
        }

        private static bool IsLeadTrigger(WorkflowTrigger trigger)
            => trigger is WorkflowTrigger.LeadCreated or WorkflowTrigger.LeadQualified or WorkflowTrigger.LeadConverted;

        private static bool IsDealTrigger(WorkflowTrigger trigger)
            => trigger is WorkflowTrigger.DealCreated or WorkflowTrigger.DealWon or WorkflowTrigger.DealLost;

        private static LeadStatus? ImpliedLeadStatusFor(WorkflowTrigger trigger)
            => trigger switch
            {
                WorkflowTrigger.LeadCreated => LeadStatus.New,
                WorkflowTrigger.LeadQualified => LeadStatus.Qualified,
                WorkflowTrigger.LeadConverted => LeadStatus.Converted,
                _ => null
            };

        private static DealStates? ImpliedDealStateFor(WorkflowTrigger trigger)
            => trigger switch
            {
                WorkflowTrigger.DealCreated => DealStates.Open,
                WorkflowTrigger.DealWon => DealStates.CloseWon,
                WorkflowTrigger.DealLost => DealStates.CloseLost,
                _ => null
            };
    }
}
