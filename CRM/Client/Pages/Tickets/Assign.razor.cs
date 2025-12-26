using CRM.Client.Helpers;
using CRM.Client.Pages.Groups;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.Models;
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

namespace CRM.Client.Pages.Tickets
{
    public partial class Assign: ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketService _service { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _usersService { get; set; }

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
        public EventCallback OnClose { get; set; }

        private List<ApplicationUser> _users = new List<ApplicationUser>();
        private List<ApplicationUser> _filteredUsers = new List<ApplicationUser>();
        private Ticket _ticket;
        private HashSet<string> _selectedUserIds = new HashSet<string>();
        private string _searchQuery = string.Empty;

        // ✅ NUOVO: Mappa del carico di lavoro per ogni utente
        private Dictionary<string, UserWorkloadInfo> _userWorkloadMap = new();
        private bool _isLoadingWorkload = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            await LoadUsers();
            
            // ✅ NUOVO: Carica il workload dopo aver caricato gli utenti
            await LoadUserWorkload();
        }

        private async Task LoadData()
        {
            _ticket = await _service.Get(Id);
            
            // ✅ NUOVO: Carica gli utenti già assegnati
            await LoadAssignedUsers();
        }

        /// <summary>
        /// Carica gli utenti già assegnati al ticket
        /// </summary>
        private async Task LoadAssignedUsers()
        {
            try
            {
                var response = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{Id}/assigned-users");
                if (response != null)
                {
                    _selectedUserIds = new HashSet<string>(response);
                }

                // Aggiungi anche l'utente principale se presente (retrocompatibilità)
                if (!string.IsNullOrEmpty(_ticket.IdUserAssigned))
                {
                    _selectedUserIds.Add(_ticket.IdUserAssigned);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");
            }
        }

        /// <summary>
        /// Carica tutti gli utenti disponibili per l'assegnazione
        /// </summary>
        private async Task LoadUsers()
        {
            try
            {
                UsersFilterModel request = new UsersFilterModel
                {
                    IdTicketToAssign = Id,
                    PageSize = 0
                };

                var response = await _usersService.GetList(request);
                _users = response.Items;
                _filteredUsers = _users;

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ NUOVO: Carica il carico di lavoro (workload) di ogni utente per la data del ticket
        /// </summary>
        private async Task LoadUserWorkload()
        {
            if (_ticket?.Date == null)
            {
                Console.WriteLine("LoadUserWorkload: Ticket.Date is null, skip workload calculation");
                return;
            }

            try
            {
                _isLoadingWorkload = true;
                StateHasChanged();

                var date = _ticket.Date.Value.Date;
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
                _filteredUsers = _users;
            }
            else
            {
                _filteredUsers = _users
                    .Where(u => u.NameComplete.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                               u.Email.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
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

            StateHasChanged();
        }

        /// <summary>
        /// Rimuove un utente dalla selezione
        /// </summary>
        private void RemoveUser(string userId)
        {
            _selectedUserIds.Remove(userId);
            StateHasChanged();
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
            try
            {
                // ✅ RIMOSSA VALIDAZIONE: Permettere zero utenti per rimuovere tutte le assegnazioni
                // La validazione "almeno 1 utente" viene rimossa per permettere di disassegnare tutti
                // if (!_selectedUserIds.Any())
                // {
                //     NotificationService.Notify(new NotificationMessage
                //     {
                //         Severity = NotificationSeverity.Warning,
                //         Summary = Localize["Warning"],
                //         Detail = Localize["Please select at least one user"],
                //         Duration = 3000
                //     });
                //     return;
                // }

                // ✅ NUOVO: Chiamata API per salvare assegnazioni multiple (anche lista vuota)
                var assignmentData = new
                {
                    ticketId = Id,
                    userIds = _selectedUserIds.ToList()
                };

                var response = await HttpClient.PostAsJsonAsync($"api/Tickets/{Id}/assign-users", assignmentData);

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
                    
                    DialogService.Close(true); // Passa true per indicare successo
                }
                else
                {
                    NotificationService.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = Localize["Error"],
                        Detail = Localize["Failed to assign users"],
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
            if (user == null || _ticket?.Date == null) return;

            // Costruisci URL con parametri query string
            var date = _ticket.Date.Value.ToString("yyyy-MM-dd");
            var baseUri = NavigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/Tickets/Schedule?userId={userId}&date={date}";
            
            // ✅ Apri in NUOVA TAB tramite JavaScript
            await JSRuntime.InvokeVoidAsync("open", url, "_blank");
        }
    }
}
