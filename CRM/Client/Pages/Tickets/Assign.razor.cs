using CRM.Client.Helpers;
using CRM.Client.Pages.Groups;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using FluentValidation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace CRM.Client.Pages.Tickets
{
    public partial class Assign: ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketsService TicketService { get; set; }

        [Inject]
        private ITicketInterventionUsersService InterventionsUserService { get; set; }


        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> UsersService { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        NotificationService NotificationService { get; set; }

        // ✅ NUOVO: Inject JSRuntime per aprire nuove tab
        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public int Id { get; set; }

        [Parameter]
        public int? TicketId { get; set; }

        [Parameter]
        public int? TicketTypeId { get; set; }

        [Parameter]
        public DateTime? Date { get; set; }

        [Parameter]
        public HashSet<string> PreselectedUserIds { get; set; }

        [Parameter]
        public EventCallback<HashSet<string>> OnUsersSelected { get; set; }

        [Parameter]
        public EventCallback OnClose { get; set; }

        // ✅ NUOVO: Flag per distinguere se viene usato per Interventi
        [Parameter]
        public bool IsForIntervention { get; set; } = false;

        private List<ApplicationUser> _users = new List<ApplicationUser>();
        private List<ApplicationUser> _filteredUsers = new List<ApplicationUser>();
        private List<ApplicationUser> _ticketAssignedUsers = new List<ApplicationUser>(); // ✅ Utenti assegnati al ticket

        private HashSet<string> _selectedUserIds = new HashSet<string>();
        private string _searchQuery = string.Empty;

        // ✅ NUOVO: Mappa del carico di lavoro per ogni utente
        private Dictionary<string, UserWorkloadInfo> _userWorkloadMap = new();
        private bool _isLoadingWorkload = false;

        /// <summary>Gli indicatori di carico sono disponibili solo con una data di riferimento (workload giornaliero).</summary>
        private bool _showWorkload => Date.HasValue;
        
        private bool _isLoadingPage = true;


        protected override async Task OnInitializedAsync()
        {
            if (PreselectedUserIds != null)
            {
                _selectedUserIds = PreselectedUserIds != null ? new HashSet<string>(PreselectedUserIds) : new HashSet<string>();
            }
            else
                await LoadAssignedUsers();

            await LoadUserAsync();
            
            _filteredUsers = _users.Where(u => !_selectedUserIds.Contains(u.Id)).ToList();
            
            await LoadUserWorkload();

            _isLoadingPage = false;
        }

        private async Task LoadData()
        {
            
            // ✅ NUOVO: Carica gli utenti già assegnati
            await LoadAssignedUsers();
        }

       

        private async Task LoadUserAsync()
        {
            if (IsForIntervention)
            {
                // Carica tutti gli utenti assegnati al ticket
                await LoadUserTicketAssigned();
            }
            else
            {
                // Carica tutti gli utenti che possono essere assegnati al tipo di ticket
                await LoadUserTicketToAssign();
            }
            _filteredUsers = _users.Where(u => !_selectedUserIds.Contains(u.Id)).ToList();
        }

        private async Task LoadUserTicketToAssign()
        {
            UsersFilterModel request = new UsersFilterModel
            {
                TicketTypeToAssign = TicketTypeId,
                PageSize = 0
            };

            var response = await UsersService.Get(request);
            _users = response.Items.ToList();
        }

        private async Task LoadUserTicketAssigned()
        {
            UsersFilterModel request = new UsersFilterModel
            {
                IdTicketAssigned = TicketId,
                PageSize = 0
            };

            var response = await UsersService.Get(request);
            _users = response.Items.ToList();
        }


        /// <summary>
        /// Carica gli utenti già assegnati al ticket
        /// </summary>
        private async Task LoadAssignedUsers()
        {
            try
            {
                if (IsForIntervention)
                {
                    _selectedUserIds = await InterventionsUserService.LoadAssignedUsers(Id);
                }
                else
                {
                    _selectedUserIds = await TicketService.LoadAssignedUsers(Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ NUOVO: Carica gli utenti assegnati al ticket (per filtrare gli interventi)
        /// </summary>
        private async Task LoadTicketAssignedUsers()
        {
            try
            {
                var ticketId =  Id;
                if (ticketId == 0) return;

                var response = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{ticketId}/assigned-users");
                if (response != null && response.Any())
                {
                    _ticketAssignedUsers = _users.Where(u => response.Contains(u.Id)).ToList();
                }
               
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti ticket: {ex.Message}");
            }
        }


        /// <summary>
        /// ✅ NUOVO: Carica il carico di lavoro (workload) di ogni utente per la data del ticket
        /// </summary>
        private async Task LoadUserWorkload()
        {
            if (Date == null)
            {
                Console.WriteLine("LoadUserWorkload: Ticket.Date is null, skip workload calculation");
                return;
            }

            try
            {
                _isLoadingWorkload = true;
                StateHasChanged();

                var date = Date.Value.Date;
                var url = $"api/Tickets/user-workload?date={date:yyyy-MM-dd}";

                Console.WriteLine($"LoadUserWorkload: Fetching from {url}");

                // Chiamata API che restituisce Dictionary<string, object>
                var response = await HttpClient.GetFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>(url);

                if (response != null)
                {
                    _userWorkloadMap.Clear();

                    foreach (var kvp in response)
                    {
                        var userId = kvp.Key;
                        var workloadData = kvp.Value;

                        // Deserializza manualmente l'oggetto
                        var workloadInfo = new UserWorkloadInfo
                        {
                            UserId = userId,
                            FullName = workloadData.GetProperty("fullName").GetString(),
                            TicketCount = workloadData.GetProperty("ticketCount").GetInt32(),
                            Tickets = System.Text.Json.JsonSerializer.Deserialize<List<TicketWorkloadItem>>(
                                workloadData.GetProperty("tickets").GetRawText())
                        };

                        _userWorkloadMap[userId] = workloadInfo;
                    }

                    Console.WriteLine($"LoadUserWorkload: Loaded workload for {_userWorkloadMap.Count} users");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento workload: {ex.Message}");
            }
            finally
            {
                _isLoadingWorkload = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Gestisce la ricerca utenti in tempo reale
        /// </summary>
        private void OnSearchChanged(string searchQuery)
        {
            _searchQuery = searchQuery?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_searchQuery))
            {
                // ✅ FIX: Escludi utenti già selezionati dalla lista disponibile
                _filteredUsers = _users
                    .Where(u => !_selectedUserIds.Contains(u.Id))
                    .ToList();
            }
            else
            {
                // ✅ FIX: Escludi utenti già selezionati + applica filtro ricerca
                _filteredUsers = _users
                    .Where(u => !_selectedUserIds.Contains(u.Id) &&
                               (u.NameComplete.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                u.Email.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            StateHasChanged();
        }

        /// <summary>
        /// Toggle selezione utente (aggiungi/rimuovi)
        /// </summary>
        private void ToggleUser(string userId)
        {
            if (_selectedUserIds.Contains(userId))
            {
                _selectedUserIds.Remove(userId);
            }
            else
            {
                _selectedUserIds.Add(userId);
            }

            // ✅ FIX: Aggiorna la lista filtrata per rimuovere/aggiungere l'utente
            OnSearchChanged(_searchQuery);
        }

        /// <summary>
        /// Rimuove un utente dalla selezione
        /// </summary>
        private void RemoveUser(string userId)
        {
            _selectedUserIds.Remove(userId);

            // ✅ FIX: Aggiorna la lista filtrata per mostrare di nuovo l'utente rimosso
            OnSearchChanged(_searchQuery);
        }

        /// <summary>
        /// Rimuove in un colpo solo tutti gli utenti selezionati.
        /// </summary>
        private void ClearAllUsers()
        {
            if (_selectedUserIds.Count == 0)
                return;

            _selectedUserIds.Clear();
            OnSearchChanged(_searchQuery);
        }

        /// <summary>
        /// Ottieni iniziali dal nome completo
        /// </summary>
        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "?";

            if (parts.Length == 1)
                return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        /// <summary>
        /// Salva le assegnazioni multiple
        /// </summary>
        protected async Task HandleValidSubmit()
        {
            HttpResponseMessage response;
            try
            {
                // ✅ FIX: Per nuovo ticket/intervento, restituisci solo gli utenti selezionati senza salvare
                if (Id == 0)
                {
                    // Restituisci gli utenti selezionati tramite callback
                    if (OnUsersSelected.HasDelegate)
                    {
                        await OnUsersSelected.InvokeAsync(_selectedUserIds);
                    }
                    
                    DialogService.Close(_selectedUserIds); // Passa gli utenti selezionati
                    return;
                }

                // ✅ Ticket/Intervento esistente: salva su server come prima
                var assignmentData = new AssignUsersRequest
                {
                    TicketId = Id,
                    UserIds = _selectedUserIds.ToList()
                };
                
                if (IsForIntervention)
                {
                    response = await InterventionsUserService.AssignUsersToIntervention(Id, assignmentData);

                    //response = await HttpClient.PostAsJsonAsync($"api/TicketInterventionUsers/intervention/{Id}/assign-users", assignmentData);
                }
                else
                {
                    
                    response = await TicketService.AssignUsers(Id, assignmentData);

                    //response = await HttpClient.PostAsJsonAsync($"api/Tickets/{Id}/assign-users", assignmentData);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    // ✅ Messaggio diverso in base al numero di utenti
                    var message = _selectedUserIds.Any()
                        ? Localize["Users assigned successfully"]
                        : Localize["All users unassigned successfully"];

                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Success,
                        Summary = Localize["Success"],
                        Detail = message,
                        Duration = 3000
                    });

                    // ✅ Chiudi il dialog e segnala successo
                    if (OnClose.HasDelegate)
                        await OnClose.InvokeAsync();
                    
                    // ✅ FIX: Restituisci sempre gli utenti selezionati invece di true
                    DialogService.Close(_selectedUserIds);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Errore API: {response.StatusCode} - {errorContent}");
                    
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = Localize["Error"],
                        Detail = $"{Localize["Failed to assign users"]}: {errorContent}",
                        Duration = 4000
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assegnazione: {ex.Message}");
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = Localize["Error"],
                    Detail = ex.Message,
                    Duration = 4000
                });
            }
        }

        
        protected void Cancel()
        {
            DialogService.Close(false); // Passa false per indicare annullamento
        }

        /// <summary>
        /// ✅ NUOVO: Ottiene le informazioni di workload per un utente
        /// </summary>
        private UserWorkloadInfo GetUserWorkload(string userId)
        {
            if (_userWorkloadMap.TryGetValue(userId, out var workload))
                return workload;

            // Default: utente senza workload (libero)
            return new UserWorkloadInfo
            {
                UserId = userId,
                TicketCount = 0
            };
        }

        /// <summary>
        /// ✅ NUOVO: Apre lo scheduler filtrato per un utente specifico in NUOVA TAB
        /// Mantiene il dialog Assign.razor aperto così l'utente può tornare facilmente
        /// </summary>
        private async Task OpenSchedulerForUser(string userId)
        {
            var user = _users.FirstOrDefault(u => u.Id == userId);
            if (user == null || Date == null) return;

            // Costruisci URL con parametri query string
            var date = Date.Value.ToString("yyyy-MM-dd");
            var baseUri = NavigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/Tickets/Schedule?userId={userId}&date={date}";
            
            // ✅ Apri in NUOVA TAB tramite JavaScript
            await JSRuntime.InvokeVoidAsync("open", url, "_blank");
        }
    }
}
