using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Initiatives
{
    [Authorize]
    public partial class Info : ComponentBase
    {
        public enum InitiativeViews
        {
            Details,
            Members,
            Schedule,
            Activities,
            Leads,
            Deals,
            Quotes,
            Orders
        }

        private enum DetailMode
        {
            Details,
            Edit
        }

        private enum DealMode
        {
            Index,
            Edit
        }

        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IInitiativeService InitiativeService { get; set; } = default!;
        [Inject] private ILeadService LeadService { get; set; } = default!;
        [Inject] private IActivityService ActivityService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;
        [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private NotificationService NotificationService { get; set; } = default!;

        [Parameter] public int Id { get; set; }

        private PageHeaderModel? _pageHeader;
        private InitiativeDTO? _initiative;
        private InitiativeSummaryDTO? _report;
        private List<InitiativeLeadTriageDTO> _triage = new();
        private bool _onlyIncomplete;

        /// <summary>Biglietti su cui c'e' ancora qualcosa da fare stasera.</summary>
        private int IncompleteCount => _triage.Count(x => x.IsIncomplete);

        /// <summary>Biglietti che somigliano a un'azienda gia' a sistema e non sono ancora collegati.</summary>
        private int SuggestedCount => _triage.Count(x => x.IdCompany == null && x.SuggestedCompanyId != null);

        private List<InitiativeLeadTriageDTO> VisibleTriage
            => _onlyIncomplete ? _triage.Where(x => x.IsIncomplete).ToList() : _triage;
        private List<InitiativeScheduleDTO> _schedules = new();
        private List<ActivityDTO> _activities = new();
        private bool _loading = true;
        private bool _scheduleSaving;
        private ButtonSize _buttonSize = ButtonSize.Medium;
        private InitiativeViews _selectedView = InitiativeViews.Details;
        private DetailMode _detailMode = DetailMode.Details;
        private DealMode _dealMode = DealMode.Index;
        private InitiativeScheduleDTO? _scheduleEdit;
        private int? _idDeal;
        private List<ViewOption<InitiativeViews>> _viewOptions = new();

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();
            await FindResponsiveness();
            InitializeViewOptions();
        }

        protected override async Task OnParametersSetAsync()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            _loading = true;
            try
            {
                _initiative = await InitiativeService.GetItemAsync(Id);
                _report = await InitiativeService.GetReportAsync(Id);
                await LoadLeadsAsync();
                _schedules = await InitiativeService.GetSchedulesAsync(Id);
                _activities = await ActivityService.GetByInitiativeAsync(Id);
            }
            finally
            {
                _loading = false;
            }
        }

        private void InitializeViewOptions()
        {
            _viewOptions = new List<ViewOption<InitiativeViews>>
            {
                new() { Text = "Dettagli", Value = InitiativeViews.Details },
                new() { Text = "Partecipanti", Value = InitiativeViews.Members },
                new() { Text = "Presenze", Value = InitiativeViews.Schedule },
                new() { Text = "Attivita", Value = InitiativeViews.Activities },
                new() { Text = "Lead", Value = InitiativeViews.Leads },
                new() { Text = "Opportunity", Value = InitiativeViews.Deals },
                new() { Text = "Preventivi", Value = InitiativeViews.Quotes },
                new() { Text = "Ordini", Value = InitiativeViews.Orders }
            };
        }

        private async Task LoadLeadsAsync()
        {
            _triage = await InitiativeService.GetLeadTriageAsync(Id);
        }

        /// <summary>
        /// Collega il biglietto all'azienda proposta. Si ricarica solo il triage: il resto del
        /// resoconto non cambia, e ricaricarlo tutto farebbe sfarfallare la pagina a ogni riga
        /// sistemata - in una serata di triage sono decine.
        /// </summary>
        private async Task LinkLead(InitiativeLeadTriageDTO lead)
        {
            if (lead.SuggestedCompanyId == null)
                return;

            if (await InitiativeService.LinkLeadToCompanyAsync(Id, lead.Id, lead.SuggestedCompanyId.Value))
                await LoadLeadsAsync();
            else
                await DialogService.Alert("Collegamento non riuscito. Riprova.", "Triage biglietti");
        }

        private void ChangeView()
        {
            _detailMode = DetailMode.Details;
            _dealMode = DealMode.Index;
            _scheduleEdit = null;
            StateHasChanged();
        }

        private async Task FindResponsiveness()
        {
            try
            {
                if (await JsRuntime.InvokeAsync<bool>("isDevice"))
                    _buttonSize = ButtonSize.Small;
            }
            catch
            {
                _buttonSize = ButtonSize.Medium;
            }
        }

        private void Back()
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/List/{_initiative?.Kind ?? InitiativeKind.Trip}");

        private void EditInline()
        {
            _selectedView = InitiativeViews.Details;
            _detailMode = DetailMode.Edit;
        }

        private async Task SaveInline()
        {
            _selectedView = InitiativeViews.Details;
            _detailMode = DetailMode.Details;
            await LoadDataAsync();
            await InvokeAsync(StateHasChanged);
        }

        private void CancelInline()
        {
            _selectedView = InitiativeViews.Details;
            _detailMode = DetailMode.Details;
            StateHasChanged();
        }

        private void NewDealInline()
        {
            _selectedView = InitiativeViews.Deals;
            _dealMode = DealMode.Edit;
            _idDeal = null;
        }

        private void EditDealInline(int id)
        {
            _selectedView = InitiativeViews.Deals;
            _dealMode = DealMode.Edit;
            _idDeal = id;
        }

        private async Task SaveDealInline()
        {
            _selectedView = InitiativeViews.Deals;
            _dealMode = DealMode.Index;
            _idDeal = null;
            await LoadDataAsync();
            await InvokeAsync(StateHasChanged);
        }

        private void CancelDealInline()
        {
            _selectedView = InitiativeViews.Deals;
            _dealMode = DealMode.Index;
            _idDeal = null;
            StateHasChanged();
        }

        private void NewSchedule()
        {
            var firstMember = _initiative?.Members.FirstOrDefault();
            var start = (_initiative?.DateFrom.Date ?? DateTime.Today).AddHours(9);
            _selectedView = InitiativeViews.Schedule;
            _scheduleEdit = new InitiativeScheduleDTO
            {
                IdInitiative = Id,
                IdUser = firstMember?.IdUser ?? string.Empty,
                Start = start,
                End = start.AddHours(8),
                Type = InitiativeScheduleType.Presence,
                Location = _initiative?.Location
            };
        }

        private void EditSchedule(InitiativeScheduleDTO item)
        {
            _selectedView = InitiativeViews.Schedule;
            _scheduleEdit = new InitiativeScheduleDTO
            {
                Id = item.Id,
                IdInitiative = item.IdInitiative,
                IdUser = item.IdUser,
                UserName = item.UserName,
                Start = item.Start,
                End = item.End,
                Type = item.Type,
                Location = item.Location,
                Notes = item.Notes,
                CreatedAt = item.CreatedAt
            };
        }

        private async Task SaveSchedule()
        {
            if (_scheduleEdit == null)
                return;

            _scheduleSaving = true;
            try
            {
                var response = await InitiativeService.SaveScheduleAsync(Id, _scheduleEdit);
                if (!response.State)
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Presenze", response.Message ?? "Salvataggio fallito");
                    return;
                }

                _scheduleEdit = null;
                await LoadDataAsync();
            }
            finally
            {
                _scheduleSaving = false;
            }
        }

        private void CancelSchedule()
        {
            _scheduleEdit = null;
        }

        private async Task DeleteSchedule(InitiativeScheduleDTO item)
        {
            if (await DialogService.Confirm($"Eliminare la presenza di {item.UserName} del {item.Start:dd/MM/yyyy HH:mm}?", "Elimina presenza") != true)
                return;

            if (!await InitiativeService.DeleteScheduleAsync(Id, item.Id))
            {
                NotificationService.Notify(NotificationSeverity.Error, "Presenze", "Impossibile eliminare la presenza");
                return;
            }

            await LoadDataAsync();
        }

        private async Task NewActivity()
        {
            var saved = await DialogService.OpenAsync<CRM.Client.Pages.Activities.ActivityAgendaDialog>(
                "Nuova attivita",
                new Dictionary<string, object?>
                {
                    ["DefaultDueDate"] = DateTime.Now.AddHours(1),
                    ["DefaultInitiativeId"] = Id
                },
                ActivityDialogOptions());

            if (saved == true)
                await LoadDataAsync();
        }

        private async Task EditActivity(int id)
        {
            var saved = await DialogService.OpenAsync<CRM.Client.Pages.Activities.ActivityAgendaDialog>(
                "Modifica attivita",
                new Dictionary<string, object?> { ["ActivityId"] = id },
                ActivityDialogOptions());

            if (saved == true)
                await LoadDataAsync();
        }

        private static DialogOptions ActivityDialogOptions() => new()
        {
            Width = "920px",
            Height = "auto",
            Resizable = true,
            Draggable = true
        };

        private void Capture()
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/{Id}/Capture");

        private void NewLead()
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}/New?idInitiative={Id}");

        private void OpenExpenses()
            => NavigationManager.NavigateTo($"/ExpenseReceipts?idInitiative={Id}");

        /// <summary>
        /// Nuova spesa gia' intestata a questa iniziativa: il contesto sta nel percorso, cosi' non
        /// va ne' digitato ne' ricordato davanti allo scontrino.
        /// </summary>
        private void NewExpense()
            => NavigationManager.NavigateTo($"/ExpenseReceipts/Create?idInitiative={Id}");

        private void OpenLead(int id)
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientLeadsPath}/{id}/Edit");

        private void OpenDeal(int id)
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientDealPath}/{id}");

        private void OpenQuote(int id)
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientQuotesPath}/{id}/Details");

        private void OpenOrder(int id)
            => NavigationManager.NavigateTo($"/{ConstHelper.ClientOrdersPath}/{id}/Details");

        private static string KindText(InitiativeKind kind) => Index.KindText(kind);

        private static string StateText(InitiativeState state) => Index.StateText(state);

        private static string StateClass(InitiativeState state) => Index.StateClass(state);

        private static string LeadStatusText(LeadStatus status) => status switch
        {
            LeadStatus.New => "Nuovo",
            LeadStatus.Contacted => "Contattato",
            LeadStatus.Qualified => "Qualificato",
            LeadStatus.Converted => "Convertito",
            LeadStatus.Lost => "Perso",
            _ => "Squalificato"
        };

        private string RoiText()
            => _report?.Roi == null ? "-" : $"{_report.Roi.Value * 100:0.#}%";

        private string CostPerLeadText()
            => _report?.CostPerLead == null ? "-" : _report.CostPerLead.Value.ToString("C");

        private static string RoleText(InitiativeMemberRole role) => role switch
        {
            InitiativeMemberRole.Stand => "Stand",
            InitiativeMemberRole.Sales => "Commerciale",
            InitiativeMemberRole.Technical => "Tecnico",
            InitiativeMemberRole.Logistics => "Logistica",
            InitiativeMemberRole.Other => "Altro",
            _ => "Partecipante"
        };

        private static string ScheduleTypeText(InitiativeScheduleType type) => type switch
        {
            InitiativeScheduleType.Travel => "Viaggio",
            InitiativeScheduleType.Stand => "Presidio stand",
            InitiativeScheduleType.Setup => "Allestimento",
            InitiativeScheduleType.InternalTask => "Attivita interna",
            InitiativeScheduleType.Other => "Altro",
            _ => "Presenza"
        };

        private static string ActivityKindText(ActivityKind kind) => kind switch
        {
            ActivityKind.Call => "Chiamata",
            ActivityKind.Email => "Email",
            ActivityKind.Meeting => "Meeting",
            ActivityKind.Task => "Task",
            _ => "Nota"
        };

        private static string ActivityStateText(ActivityState state) => state switch
        {
            ActivityState.Done => "Completata",
            ActivityState.Cancelled => "Annullata",
            _ => "Pianificata"
        };
    }
}
