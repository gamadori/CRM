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

        private TicketDashBoardModel _model = null;
        private string _currentUserId = null;

        private string _userId;

        private List<ApplicationUser> _users;

        private AverageFeedbackDTO _averageFeedback = null;

        // Appuntamenti (attività) del giorno / prossimo appuntamento
        private List<CalendarItemDTO> _todayAppointments = new();
        private CalendarItemDTO _nextAppointment = null;

        protected override async Task OnInitializedAsync()
        {
            DynamicNotificationHandlers.Register(this);

            await GetCurrentUser();
            _userId = _currentUserId;
            await LoadUsers();
            await LoadData();
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
                    IdUser = _userId,
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

        protected void TicketsNewMessage()
        {
            NavigationManager.NavigateTo(Url($"/Tickets/Index/{(int)TicketTypeSearch.NewMessage}"));
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

        protected async void OnChangeIdUser()
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
    }
}
