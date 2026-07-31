using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Client.Shared.Components;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketInterventions
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IBaseRestService<TicketIntervention, TicketInterventionFilter, int> _service { get; set; }

       
        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _userService { get; set; }

        [Inject]
        private HttpClient _httpClient { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        [Inject]
        ITicketsService TicketService { get; set; }

        [Inject]
        IInterventionTypesService InterventionTypesService { get; set; }

        [Inject]
        ITicketInterventionUsersService InterventionUsersService { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public int IdTicket { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private TicketIntervention _ticketIntervention = new TicketIntervention();

        private List<Article> _products = new List<Article>();
        
        private List<TicketType> _ticketTypes = new List<TicketType>();
        
        private List<InterventionTypeDTO> _interventionTypes;
        
        private List<ApplicationUser> _users = new List<ApplicationUser>();

        private HashSet<string> _selectedUserIds = new HashSet<string>();
        
        private Ticket _ticket;
        
        private string _header;
        
        private List<TicketInterventionTimeModel> _interventionTimes = new List<TicketInterventionTimeModel>();
        
        private SignaturePad _signaturePad;
        
        private bool _signatureLoaded = false;
       
        protected override async Task OnInitializedAsync()
        {
            try
            {

                

                if (Id != null)
                {
                    _header = "INTERVENTO MODIFICA";
                    _ticketIntervention = await _service.Get(Id.Value);
                    await LoadInterventionAssignedUsers();
                    IdTicket = _ticketIntervention.IdTicket;
                    _ticket = await TicketService.Get(IdTicket);
                    StateHasChanged();
                }
                else
                {
                    _header = "INTERVENTO NUOVO";
                    _ticketIntervention = new TicketIntervention() { 
                        StartDateTime = DateTime.Now, 
                        EndDateTime = DateTime.Now
                    };
                    _ticket = await TicketService.Get(IdTicket);
                    _ticketIntervention.IdTicket = IdTicket;
                    _ticketIntervention.SupportType = _ticket.IdCommessaFase.HasValue
                        ? (int)TypesSupport.Workshop
                        : (int)TypesSupport.Phone;

                    await PreselectDefaultUserAsync();
                }
                await LoadUsers();

                await GetTicketTypes();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Precompila l'intervento con chi e' gia' in carico sul ticket: prima il principale
        /// (IdUserAssigned), altrimenti il primo degli assegnati.
        /// <para>
        /// Su un ticket smistato solo a un gruppo non c'e' nessuno da precompilare e la selezione
        /// resta vuota: la fa l'operatore nel dialog, dove i membri del gruppo sono candidati e il
        /// salvataggio vale come presa in carico.
        /// </para>
        /// </summary>
        private async Task PreselectDefaultUserAsync()
        {
            if (!string.IsNullOrEmpty(_ticket?.IdUserAssigned))
            {
                _selectedUserIds.Add(_ticket.IdUserAssigned);
                return;
            }

            try
            {
                var assignedUserIds = await TicketService.LoadAssignedUsers(IdTicket);

                if (assignedUserIds != null && assignedUserIds.Any())
                    _selectedUserIds.Add(assignedUserIds.First());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati al ticket: {ex.Message}");
            }
        }

        private async Task LoadInterventionAssignedUsers()
        {
            try
            {
               
                _selectedUserIds = await InterventionUsersService.LoadAssignedUsers(Id.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti intervento: {ex.Message}");
            }
        }

        private async Task OpenAssignUsersDialog()
        {
            Console.WriteLine($"[OpenAssignUsersDialog] Inizio - _ticket is null: {_ticket == null}");
            
            // ✅ Verifica che _ticket sia caricato
            if (_ticket == null)
            {
                Console.WriteLine("[OpenAssignUsersDialog] ERRORE: Ticket non ancora caricato");
                return;
            }

          

            var result = await DialogService.OpenAsync<Tickets.Assign>("Assegna Utenti all'Intervento",
                new Dictionary<string, object>
                {
                    { "Id", _ticketIntervention?.Id },
                    {"TicketTypeId", _ticket.IdType },
                    {"TicketId", _ticket.Id },
                    { "Date", _ticket.Date },
                    { "PreselectedUserIds", new HashSet<string>(_selectedUserIds) },
                    { "IsForIntervention", true }
                },
                new DialogOptions
                {
                    Width = "700px",
                    Height = "600px",
                    Resizable = true,
                    Draggable = true
                });

            Console.WriteLine($"[OpenAssignUsersDialog] Dialog chiuso - result type: {result?.GetType().Name ?? "null"}");

            if (result is HashSet<string> updatedUserIds)
            {
                Console.WriteLine($"[OpenAssignUsersDialog] Utenti aggiornati: {updatedUserIds.Count}");
                _selectedUserIds = updatedUserIds;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Carica i nomi con cui risolvere gli utenti selezionati: gli stessi candidati del dialog
        /// (assegnati al ticket + membri del gruppo assegnato), senza paginazione.
        /// <para>
        /// Prima era una pagina qualsiasi di tutti gli utenti (PageSize di default = 10): un
        /// selezionato fuori da quei dieci non veniva risolto e la UI mostrava "Nessun utente
        /// selezionato" pur avendo la selezione valorizzata.
        /// </para>
        /// </summary>
        private async Task LoadUsers()
        {
            UsersFilterModel request = new UsersFilterModel
            {
                IdTicketAssigned = IdTicket,
                IncludeGroupMembers = true,
                PageSize = 0
            };

            var response = await _userService.Get(request);
            _users = response?.Items?.ToList() ?? new List<ApplicationUser>();
            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
            try
            {
                if (_selectedUserIds == null || !_selectedUserIds.Any())
                {
                    // Un intervento senza persone non e' registrabile: dirlo, non solo loggarlo.
                    NotificationService?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Warning,
                        Summary = Localize["Warning"],
                        Detail = Localize["Select at least one user for the intervention"],
                        Duration = 5000
                    });
                    return;
                }

                if (_signaturePad != null)
                {
                    var signatureBase64 = await _signaturePad.GetSignatureAsync();
                    if (!string.IsNullOrWhiteSpace(signatureBase64))
                    {
                        _ticketIntervention.CustomerSignature = signatureBase64;
                    }
                }

                if (Id == null)
                {
                    _ticketIntervention.IdTicket = IdTicket;
                }

                var resp = await _service.Post(_ticketIntervention);
                if (resp != null && resp.State)
                {
                    if (resp.Data != null)
                        _ticketIntervention = resp.Data;

                    // L'assegnazione porta con se' la presa in carico del ticket e puo' essere
                    // rifiutata (utente non abilitato): non si esce senza dirlo, altrimenti
                    // l'intervento resterebbe salvato ma senza nessuno a cui e' imputato.
                    if (!await SaveUserAssignments(_ticketIntervention.Id))
                        return;

                    // Salva gli intervalli di tempo se presenti (per nuovi interventi)
                    await SaveInterventionTimes(_ticketIntervention.Id);

                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo("/TicketsInterventions/Index");
                }
                else
                {
                    var errorMessage = resp != null ? $"Errore salvataggio intervento: {resp.Message}" : "Errore sconosciuto durante il salvataggio dell'intervento.";

                    Console.WriteLine(errorMessage);
                    
                    NotificationService?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "Errore",
                        Detail = errorMessage,
                        Duration = 6000
                    });
                }
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        /// <summary>
        /// Salva gli intervalli di tempo in memoria dopo il salvataggio dell'intervento
        /// </summary>
        private async Task SaveInterventionTimes(int interventionId)
        {
            if (_interventionTimes == null || !_interventionTimes.Any())
                return;

            // Salva solo gli elementi con Id negativo (non ancora persistiti)
            var unsavedTimes = _interventionTimes.Where(t => t.Id <= 0).ToList();
            
            foreach (var time in unsavedTimes)
            {
                try
                {
                    var newTime = new TicketInterventionTime
                    {
                        IdTicketIntervention = interventionId,
                        StartDateTime = time.StartDateTime,
                        EndDateTime = time.EndDateTime,
                        TimeType = time.TimeType,
                        Notes = time.Notes,
                        IsBillable = time.IsBillable,
                        TravelKilometers = time.TravelKilometers
                    };

                    var response = await _httpClient.PostAsJsonAsync("api/TicketInterventionTime", newTime);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var created = await response.Content.ReadFromJsonAsync<TicketInterventionTime>();
                        if (created != null)
                        {
                            time.Id = created.Id;
                            time.IdTicketIntervention = interventionId;
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Errore salvataggio tempo intervento: {response.StatusCode} - {errorContent}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore salvataggio tempo intervento: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Assegna gli utenti all'intervento. Il server, nella stessa chiamata, assegna al ticket
        /// chi non lo era ancora (presa in carico implicita) e puo' rifiutare chi non e' abilitato.
        /// </summary>
        /// <returns>False se l'assegnazione e' stata rifiutata: il chiamante non deve proseguire.</returns>
        private async Task<bool> SaveUserAssignments(int interventionId)
        {
            try
            {
                // ✅ Il controller usa l'ID dalla route, quindi invia SOLO UserIds nel body
                var assignmentData = new { UserIds = _selectedUserIds.ToList() };

                var resp = await _httpClient.PostAsJsonAsync($"api/TicketInterventionUsers/intervention/{interventionId}/assign-users", assignmentData);

                if (!resp.IsSuccessStatusCode)
                {
                    var errorContent = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"Errore salvataggio assegnazioni: {resp.StatusCode} - {errorContent}");

                    NotificationService?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = Localize["Error"],
                        Detail = string.IsNullOrWhiteSpace(errorContent)
                            ? Localize["Failed to assign users"]
                            : errorContent,
                        Duration = 8000
                    });

                    return false;
                }

                await NotifyImplicitClaimAsync(resp);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio assegnazioni: {ex.Message}");

                NotificationService?.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = Localize["Error"],
                    Detail = ex.Message,
                    Duration = 6000
                });

                return false;
            }
        }

        /// <summary>
        /// Se il salvataggio ha assegnato qualcuno al ticket, lo dichiara: e' un effetto collaterale
        /// e l'operatore deve saperlo. Un corpo inatteso non fa fallire nulla.
        /// </summary>
        private async Task NotifyImplicitClaimAsync(HttpResponseMessage response)
        {
            try
            {
                using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

                if (payload == null
                    || !payload.RootElement.TryGetProperty("claimedUsers", out var claimed)
                    || claimed.ValueKind != JsonValueKind.Array)
                    return;

                var names = claimed.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (!names.Any())
                    return;

                NotificationService?.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Info,
                    Summary = Localize["Ticket taken in charge by"],
                    Detail = string.Join(", ", names),
                    Duration = 6000
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Risposta assegnazione senza dettaglio presa in carico: {ex.Message}");
            }
        }

        private void ValueStartDateTimeChangeHandler()
        {
            if (_ticketIntervention.StartDateTime < _ticketIntervention.EndDateTime)
            {
                _ticketIntervention.Minute = (int)(_ticketIntervention.EndDateTime - _ticketIntervention.StartDateTime).TotalMinutes;
            }
        }

        protected void ValueEndDateTimeChangeHandler()
        {
            if (_ticketIntervention.EndDateTime > _ticketIntervention.StartDateTime)
            {
                _ticketIntervention.Minute = (int)(_ticketIntervention.EndDateTime - _ticketIntervention.StartDateTime).TotalMinutes;
            }
        }

        protected async Task GetTicketTypes()
        {
            _interventionTypes = await InterventionTypesService.GetListAsync(null);
            
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Tickets/Index");
        }

        private TicketInterventionArticleModel ArticlesFind(TicketInterventionArticleModel item)
        {
            return _ticketIntervention.InterventionArticles.Where(x => x.Id == item.Id).FirstOrDefault();
        }

        private void ArticlesOnDelete(TicketInterventionArticleModel item)
        {
            StateHasChanged();
        }

        private void ArticledOnAdd(TicketInterventionArticleModel item)
        {
            TicketInterventionArticleModel article = ArticlesFind(item);
            if (article != null)
                _ticketIntervention.InterventionArticles.Add(article);
        }

        private void ArticledOnUpdate(TicketInterventionArticleModel item)
        {
            TicketInterventionArticleModel article = ArticlesFind(item);
            if (article != null)
            {
                article.IdArticle = item.IdArticle;
                article.IdProduct = item.IdProduct;               
                article.SerialNumber = item.SerialNumber;
            }
        }

        private void OnUpdateArticles()
        {
            StateHasChanged();
        }

        private void OnSignatureCleared()
        {
            _ticketIntervention.CustomerSignature = null;
            StateHasChanged();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!_signatureLoaded && 
                _signaturePad != null && 
                _ticketIntervention != null && 
                !string.IsNullOrWhiteSpace(_ticketIntervention.CustomerSignature))
            {
                try
                {
                    await _signaturePad.SetSignatureAsync(_ticketIntervention.CustomerSignature);
                    _signatureLoaded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore caricamento firma: {ex.Message}");
                }
            }
            
            await base.OnAfterRenderAsync(firstRender);
        }
    }
}
