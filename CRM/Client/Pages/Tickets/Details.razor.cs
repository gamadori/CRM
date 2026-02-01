using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
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
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private ITicketService _service { get; set; }

       
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        IJSRuntime JSRuntime { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }  

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public Action OnClickTicketClose { get; set; }

        [Parameter]
        public EventCallback OnClickPrint { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        [Parameter]
        public bool HeaderVisible { get; set; } = false;

        [Parameter]
        public string BackUrl { get; set; }

        [Parameter]
        public int? IdCompany { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;


        private bool _isDownloadingPdf = false;

        private TicketModel _ticket = null;

        private List<ApplicationUser> _assignedUsers = new List<ApplicationUser>();

        private bool _isLoadingUsers = false;

        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
            _pageHeader = await HeaderService.Create(PageMode);

            StateHasChanged();
        }

       
        private async Task LoadData()
        {
            try
            {
                if (Id != null)
                {
                    _ticket = await _service.GetDetails(Id.Value);
                    
                    // ? NUOVO: Carica gli utenti assegnati dopo aver caricato il ticket
                    await LoadAssignedUsers();
                }
                else
                    _ticket = new TicketModel();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            finally
            {
                await InvokeAsync(StateHasChanged);
            }
        }

        /// <summary>
        /// ? NUOVO: Carica la lista completa degli utenti assegnati al ticket
        /// </summary>
        private async Task LoadAssignedUsers()
        {
            if (Id == null) return;

            try
            {
                _isLoadingUsers = true;
                
                // ? IMPORTANTE: Svuota sempre la lista prima di ricaricare
                _assignedUsers.Clear();
                await InvokeAsync(StateHasChanged); // Forza render con lista vuota
                
                // ? DEBUG: Log chiamata API
                Console.WriteLine($"[Details.LoadAssignedUsers] Chiamata API: api/Tickets/{Id}/assigned-users");
                
                // Ottieni gli ID degli utenti assegnati
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{Id}/assigned-users");
                
                // ? DEBUG: Log risposta
                Console.WriteLine($"[Details.LoadAssignedUsers] Ricevuti {userIds?.Count ?? 0} ID utenti: {string.Join(", ", userIds ?? new List<string>())}");
                
                if (userIds != null && userIds.Any())
                {
                    // Carica i dettagli completi degli utenti
                    foreach (var userId in userIds)
                    {
                        try
                        {
                            Console.WriteLine($"[Details.LoadAssignedUsers] Caricamento dati utente: {userId}");
                            var user = await HttpClient.GetFromJsonAsync<ApplicationUser>($"api/Users/{userId}");
                            
                            if (user != null)
                            {
                                Console.WriteLine($"[Details.LoadAssignedUsers] Utente caricato: {user.NameComplete} (ID: {user.Id})");
                                _assignedUsers.Add(user);
                            }
                            else
                            {
                                Console.WriteLine($"[Details.LoadAssignedUsers] ATTENZIONE: Utente {userId} non trovato (null)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Details.LoadAssignedUsers] ERRORE caricamento utente {userId}: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"[Details.LoadAssignedUsers] Totale utenti caricati: {_assignedUsers.Count}");
                }
                else
                {
                    Console.WriteLine($"[Details.LoadAssignedUsers] Nessun utente assegnato al ticket #{Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Details.LoadAssignedUsers] ERRORE caricamento utenti assegnati: {ex.Message}");
                // ? In caso di errore, assicurati che la lista sia vuota
                _assignedUsers.Clear();
            }
            finally
            {
                _isLoadingUsers = false;
                // ? IMPORTANTE: Forza sempre il render finale
                await InvokeAsync(StateHasChanged);
                
                Console.WriteLine($"[Details.LoadAssignedUsers] Completato. _isLoadingUsers=false, _assignedUsers.Count={_assignedUsers.Count}");
            }
        }

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Tickets/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                BackToUrl();
            //NavigationManager.NavigateTo("/Tickets/Index");
        }

        protected void TicketClose()
        {
            if (OnClickTicketClose != null)
                OnClickTicketClose();
            else
                NavigationManager.NavigateTo($"/Tickets/Close/{Id}");
        }

        private async void TicketPrint()
        {
            if (OnClickPrint.HasDelegate)
                await OnClickPrint.InvokeAsync();
            else
                NavigationManager.NavigateTo($"/Tickets/Report/{Id}");
        }

        /// <summary>
        /// Scarica il PDF del ticket con QuestPDF
        /// </summary>
        private async Task DownloadPdf()
        {
            try
            {
                if (Id == null || Id <= 0)
                    return;

                _isDownloadingPdf = true;
                StateHasChanged();

                // Chiamata API per ottenere il PDF
                var response = await HttpClient.GetAsync($"api/Tickets/pdf/{Id}");

                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync();
                    var fileName = $"Ticket_{Id}_{DateTime.Now:yyyyMMdd}.pdf";

                    // Scarica il file nel browser
                    await JSRuntime.InvokeVoidAsync("downloadFileFromBytes", 
                        fileName, 
                        "application/pdf", 
                        fileBytes);
                }
                else
                {
                    // Mostra errore nella console (o usa un toast/notification service se disponibile)
                    Console.WriteLine($"Errore download PDF: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore download PDF: {ex.Message}");
            }
            finally
            {
                _isDownloadingPdf = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// ? NUOVO: Apre il dialog di assegnazione utenti
        /// </summary>
        private async Task PrepareAssign()
        {
            var result = await DialogService.OpenAsync<Assign>(
                Localize["Assign Users"],
                new Dictionary<string, object> { { "Id", Id } },
                new DialogOptions 
                { 
                    Width = "900px", 
                    Height = "650px",
                    Resizable = true,
                    Draggable = true,
                    CloseDialogOnEsc = true
                }
            );

            // ? DEBUG: Log per verificare il risultato
            Console.WriteLine($"[Details] Dialog chiuso. Result: {result}");
            
            // ? IMPORTANTE: Ricarica SEMPRE i dati dopo la chiusura del dialog
            // Questo garantisce che l'UI sia sincronizzata anche se tutti gli utenti sono rimossi
            Console.WriteLine($"[Details] Ricaricamento dati ticket #{Id}...");
            await LoadData();
            
            Console.WriteLine($"[Details] Utenti assegnati dopo reload: {_assignedUsers.Count}");
            await InvokeAsync(StateHasChanged);
        }

        protected void SendInvitation()
        {

        }

        /// <summary>
        /// ? NUOVO: Ottiene le iniziali dal nome completo dell'utente
        /// </summary>
        private string GetUserInitials(string fullName)
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
        /// Calcola il colore del testo (bianco o nero) in base alla luminosità del colore di sfondo
        /// </summary>
        private string GetContrastTextColor(string backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
                return "#000000";

            var hex = backgroundColor.TrimStart('#');


            if (hex.Length == 3)
            {
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
            }


            if (hex.Length != 6)
                return "#000000";

            try
            {
                var r = Convert.ToInt32(hex.Substring(0, 2), 16);
                var g = Convert.ToInt32(hex.Substring(2, 2), 16);
                var b = Convert.ToInt32(hex.Substring(4, 2), 16);

                var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

                return luminance > 0.5 ? "#000000" : "#ffffff";
            }
            catch
            {
                return "#000000";
            }
        }

        /// <summary>
        /// Restituisce il colore di sfondo originale
        /// </summary>
        private string GetContrastBackgroundColor(string backgroundColor)
        {
            return backgroundColor ?? "#6c757d";
        }

        private void BackToUrl()
        {
            if (BackUrl == null || BackUrl.Length == 0)
            {
                BackUrl = "/Tickets/Index";
            }
            else
                BackUrl = BackUrl.Replace("-", "/");

            NavigationManager.NavigateTo($"/Tickets/Index/{BackUrl}");
        }
    }
}
