using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Resources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Localization;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Program;

namespace CRM.Client.Pages.DashBoard
{
    [Authorize(Policy = "StandardRole")]
    
    public partial class Index : ComponentBase, INotificationHandler<MsgNotify>, INotificationHandler<InboundEmailNotify>, IDisposable
    {
        [Inject]
        HttpClient Http { get; set; }   

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        IReportService<TicketDashBoardModel, TicketDashBoardModelFilter> _service { get; set; }

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUser { get; set; }

        [Inject]
        ITicketFeedbackService _ticketFeedbackService { get; set; }

        [Inject]
        ICalendarService _calendarService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService dialogService { get; set; }

        [Inject]
        AuthenticationStateProvider AuthenticationStateProvider { get; set; }

        [Inject]
        IAuthorizationService AuthorizationService { get; set; }

        private TicketDashBoardModel _model = null;
        private string _currentUserId = null;

        /// <summary>
        /// Admin o SuperUser: sono gli unici che vedono la situazione globale e che possono
        /// spostarsi sulla dashboard di un altro utente. Va deciso dai ruoli e non da
        /// <see cref="TicketDashBoardModel.IsClient"/>, che arriva solo dopo la prima
        /// chiamata al server mentre qui serve prima, per sapere che cosa chiedere.
        /// </summary>
        private bool _isSupervisor;

        /// <summary>Utente di cui si sta guardando la dashboard. Null = situazione globale.</summary>
        private string _userId;

        private List<ApplicationUser> _users;

        private AverageFeedbackDTO _averageFeedback = null;

        // Appuntamenti (attività) del giorno / prossimo appuntamento
        private List<CalendarItemDTO> _todayAppointments = new();
        private CalendarItemDTO _nextAppointment = null;

        private int OpenTicketsCount => (_model?.TicketsWorking ?? 0) + (_model?.TicketsNotAssigned ?? 0);

        private int OpenLoadPercent => Math.Min(100, OpenTicketsCount * 4);

        private string OperationalSubtitle
        {
            get
            {
                if (_model == null)
                    return "Situazione non disponibile";

                var criticalCount = (_model.TicketsExpired > 0 ? 1 : 0)
                    + (_model.TicketsNotAssigned > 0 ? 1 : 0)
                    + (_model.InboundEmailsToHandle > 0 ? 1 : 0)
                    + (_model.ChatMessageToRead > 0 ? 1 : 0)
                    + (_model.BlockedTickets > 0 ? 1 : 0)
                    + (_model.LateExpectedCommesse > 0 ? 1 : 0)
                    + (_model.InterventionsPendingSignature > 0 ? 1 : 0)
                    + (_model.UsersNeedConfirm > 0 ? 1 : 0);

                return criticalCount == 0
                    ? "Nessuna anomalia urgente rilevata"
                    : $"{criticalCount} aree richiedono attenzione";
            }
        }

        private IEnumerable<OperationalItem> OperationalItems => BuildOperationalItems();

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            await GetCurrentUser();

            _isSupervisor = await IsSupervisorAsync();

            // Admin e SuperUser aprono sulla situazione globale e poi scelgono l'utente dal
            // combo. Per gli altri ruoli il filtro personale e' l'unica vista possibile: il
            // server la impone comunque, qui serve solo perche' agenda e link alle liste
            // seguano lo stesso utente.
            _userId = _isSupervisor ? null : _currentUserId;

            // L'elenco utenti alimenta solo il combo: senza combo e' una chiamata sprecata.
            if (_isSupervisor)
                await LoadUsers();

            await LoadData();
        }

        /// <summary>
        /// La policy SuperUserRole raccoglie Admin e SuperUser: i ruoli sono gia' nei claim,
        /// quindi la verifica e' locale e non costa una chiamata al server.
        /// </summary>
        private async Task<bool> IsSupervisorAsync()
        {
            var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var result = await AuthorizationService.AuthorizeAsync(state.User, ePolicy.SuperUserRole.ToString());

            return result.Succeeded;
        }

        public async Task Handle(MsgNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            var id = notification.Id;
            var sender = notification.Sender;
            await LoadData();
        }

        public async Task Handle(InboundEmailNotify notification, System.Threading.CancellationToken cancellationToken)
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            TicketDashBoardModelFilter filter = new TicketDashBoardModelFilter();
            filter.IdUser = _userId;
            _model = await _service.Get(filter);
            _averageFeedback = await _ticketFeedbackService.AverageRateAsync();
            await LoadAppointments();
            StateHasChanged();
        }

        /// <summary>
        /// Carica gli appuntamenti (attività) dell'utente selezionato: quelli di oggi
        /// dall'orario corrente in poi, altrimenti il prossimo appuntamento in programma.
        /// </summary>
        private async Task LoadAppointments()
        {
            _todayAppointments = new();
            _nextAppointment = null;

            try
            {
                var now = DateTime.Now;

                var agenda = await _calendarService.GetAgendaAsync(new CalendarFilter
                {
                    DateFrom = DateTime.Today,
                    DateTo = DateTime.Today.AddDays(60),
                    // Nella vista globale non esiste un'agenda "di tutti" che abbia senso qui:
                    // si mostrano gli appuntamenti di chi sta guardando, e quelli dell'utente
                    // scelto non appena ne viene selezionato uno dal combo.
                    IdUser = _userId ?? _currentUserId,
                    Scope = CalendarScope.User,
                    IncludeActivities = true,
                    IncludeTickets = false
                });

                if (agenda?.Items == null || !string.IsNullOrWhiteSpace(agenda.ErrorMessage))
                    return;

                var upcoming = agenda.Items
                    .Where(i => !i.IsCompleted && i.Start >= now)
                    .OrderBy(i => i.Start)
                    .ToList();

                var todayEnd = DateTime.Today.AddDays(1);
                _todayAppointments = upcoming
                    .Where(i => i.Start < todayEnd)
                    .ToList();

                if (!_todayAppointments.Any())
                    _nextAppointment = upcoming.FirstOrDefault();
            }
            catch
            {
                // Non bloccare la dashboard se l'agenda non è disponibile
            }
        }

        protected void AddTicket()
        {
            NavigationManager.NavigateTo("/Tickets/Create");
        }

        protected void TicketWorking()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.Working}"));
        }

        protected void TicketExpired()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.Expired}"));
        }

        protected void TicketNotAssigned()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.NotAssigned}");
        }

        /// <summary>
        /// Ticket assegnati a un gruppo e ancora senza responsabile: il filtro non passa da
        /// <see cref="Url"/> perche' questi ticket non hanno un assegnatario su cui filtrare.
        /// </summary>
        protected void TicketsToClaim()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.ToClaim}");
        }

        /// <summary>
        /// Porta alla posta in arrivo delle chat invece che all'elenco dei ticket filtrato:
        /// l'anteprima dei messaggi dice subito cosa e' stato scritto e da chi.
        /// </summary>
        protected void TicketsNewMessage()
        {
            NavigationManager.NavigateTo("/Tickets/Messages");
        }

        protected void TicketsBlocked()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.Blocked}");
        }

        protected void LateExpectedCommesse()
        {
            NavigationManager.NavigateTo("/Commesse?ExpectedLate=true");
        }

        protected void InboundEmailsToHandle()
        {
            NavigationManager.NavigateTo("/Settings/InboundEmails");
        }

        protected void TicketAll()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.All}"));
        }

        protected void Schedule()
        {
            NavigationManager.NavigateTo("/Tickets/Schedule");
        }

        protected void TicketsSearch()
        {
            NavigationManager.NavigateTo("/Tickets/Search");
        }

        protected void GoToAgenda()
        {
            NavigationManager.NavigateTo("/Agenda");
        }

        /// <summary>
        /// Icona material da mostrare per un appuntamento (attività).
        /// </summary>
        private string AppointmentIcon(CalendarItemDTO item)
        {
            return item.ActivityKind.HasValue
                ? ActivityUi.Icon(item.ActivityKind.Value)
                : "event";
        }

        protected void TicketsToInvoice()
        {
            NavigationManager.NavigateTo($"/Tickets/Index/{(int)TicketTypeSearch.ToBeInvoiced}");
        }

        protected void UsersNeedConfirm()
        {
            NavigationManager.NavigateTo($"/Settings/Users/true");
        }

        protected void InterventionsPendingSignature()
        {
            NavigationManager.NavigateTo("/TicketsIntervention/PendingSignatures");
        }

        protected async Task OnClickNew1()
        {
            await dialogService.Confirm($"{Localize["Confermare l'utente"]}", Localize["Conferma Utente"], null);
        }

        /// <summary>
        /// Naviga alla pagina dei feedback
        /// </summary>
        protected void ViewAllFeedbacks()
        {
            NavigationManager.NavigateTo("/TicketFeedbacks");
        }

        /// <summary>
        /// Naviga ai dettagli del ticket (e segna il feedback come letto)
        /// </summary>
        protected async Task ViewTicketDetails(int ticketId)
        {
            try
            {
                var feedback = _model.RecentFeedbacks?.FirstOrDefault(f => f.TicketId == ticketId);
                if (feedback != null && !feedback.IsRead)
                {
                    await Http.PutAsync($"api/TicketFeedback/{feedback.Id}/read", null);
                }
            }
            catch
            {
                // Ignora errori nel segnare come letto
            }

            NavigationManager.NavigateTo($"/Tickets/{ticketId}/Details");
        }

        /// <summary>
        /// Tronca il nome dell'azienda se troppo lungo
        /// </summary>
        private string TruncateCompanyName(string name, int maxLength = 25)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "-";
            
            return name.Length <= maxLength ? name : name.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Restituisce un colore in base al rating (rosso-giallo-verde)
        /// </summary>
        private string GetRatingColor(decimal rating)
        {
            return rating switch
            {
                >= 4.5m => "#22c55e", // Verde scuro - Eccellente
                >= 4.0m => "#84cc16", // Verde chiaro - Ottimo
                >= 3.5m => "#eab308", // Giallo - Buono
                >= 3.0m => "#f97316", // Arancione - Sufficiente
                >= 2.0m => "#ef4444", // Rosso - Scarso
                _ => "#dc2626"        // Rosso scuro - Pessimo
            };
        }

        /// <summary>
        /// Genera il testo del pulsante "Vedi Tutte"
        /// </summary>
        private string GetViewAllButtonText()
        {
            if (_averageFeedback?.Companies == null)
                return "Vedi Tutte";
            
            return $"Vedi Tutte ({_averageFeedback.Companies.Count} aziende)";
        }

        private async Task GetCurrentUser()
        {
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity.IsAuthenticated)
            {
                _currentUserId = user.Claims.Where(a => a.Type == "sub").Select(a => a.Value).FirstOrDefault();
            }
            else
            {
                _currentUserId = null;
            }
        }

        private async Task<List<ApplicationUser>> LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel();

            var response = await _serviceUser.GetList(request);

            _users = response.Items.ToList();

            return _users;
        }

        /// <summary>
        /// Cambio utente dal combo. Svuotando la selezione <c>_userId</c> torna null e la
        /// dashboard riprende la situazione globale.
        /// </summary>
        protected async Task OnChangeIdUser()
        {
            await LoadData();
        }

        public void Dispose()
        {
            DynamicNotificationHandlers.Unregister(this);
        }

        private string Url(string url)
        {
            try
            {
                if (_userId != null && _userId.Length > 0) 
                    return $"{url}/{_userId}";
                else
                    return url;
            }
            catch (Exception ex)
            {
                return url;
            }
        }

        private async Task ReloadDashboard() 
        {
            await LoadData();
        }

        private IEnumerable<OperationalItem> BuildOperationalItems()
        {
            if (_model == null)
                return Enumerable.Empty<OperationalItem>();

            var items = new List<OperationalItem>();

            if (_model.TicketsExpired > 0)
                items.Add(new("Ticket scaduti", "Apri la coda piu urgente", _model.TicketsExpired, "schedule", "ops-danger", TicketExpired, 1));

            if (_model.TicketsNotAssigned > 0)
                items.Add(new("Da assegnare", "Ticket senza responsabile", _model.TicketsNotAssigned, "assignment_late", "ops-warning", TicketNotAssigned, 2));

            if (_model.InboundEmailsToHandle > 0)
                items.Add(new("Email in ingresso", "Da trasformare o archiviare", _model.InboundEmailsToHandle, "move_to_inbox", "ops-info", InboundEmailsToHandle, 3));

            if (_model.ChatMessageToRead > 0)
                items.Add(new("Messaggi ticket", "Conversazioni non lette", _model.ChatMessageToRead, "chat", "ops-primary", TicketsNewMessage, 4));

            if (_model.BlockedTickets > 0)
                items.Add(new("Ticket bloccati", "Completamento impedito", _model.BlockedTickets, "report_problem", "ops-danger", TicketsBlocked, 5));

            if (_model.LateExpectedCommesse > 0)
                items.Add(new("Commesse in ritardo", "Previsione oltre consegna", _model.LateExpectedCommesse, "event_busy", "ops-danger", LateExpectedCommesse, 6));

            if (_model.InterventionsPendingSignature > 0)
                items.Add(new("Firme pending", "Interventi da chiudere", _model.InterventionsPendingSignature, "draw", "ops-orange", InterventionsPendingSignature, 7));

            if (_model.UsersNeedConfirm > 0)
                items.Add(new("Utenti da confermare", "Nuovi accessi in attesa", _model.UsersNeedConfirm, "person_add", "ops-violet", UsersNeedConfirm, 8));

            return items
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.Value)
                .Take(6)
                .ToList();
        }

        private sealed record OperationalItem(
            string Title,
            string Caption,
            int Value,
            string Icon,
            string CssClass,
            Action Click,
            int Priority);
    }
}
