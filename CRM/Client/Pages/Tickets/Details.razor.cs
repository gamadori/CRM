using CRM.Client.Helpers;
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

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

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


        private bool _isDownloadingPdf = false;

        private TicketModel _ticket = null;

        // ? NUOVO: Lista degli utenti assegnati
        private List<ApplicationUser> _assignedUsers = new List<ApplicationUser>();
        private bool _isLoadingUsers = false;


       


        protected override async Task OnInitializedAsync()
        {
            if (Id == null && IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
            }
       
            await LoadData();
        }

        protected override async Task OnParametersSetAsync()
        {
            if (IdTicket != null && int.TryParse(IdTicket.ToString(), out int id))
            {
                Id = id;
                await LoadData();
            }
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
                
                // Ottieni gli ID degli utenti assegnati
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{Id}/assigned-users");
                
                if (userIds != null && userIds.Any())
                {
                    // Carica i dettagli completi degli utenti
                    foreach (var userId in userIds)
                    {
                        try
                        {
                            var user = await HttpClient.GetFromJsonAsync<ApplicationUser>($"api/Users/{userId}");
                            if (user != null)
                            {
                                _assignedUsers.Add(user);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Errore caricamento utente {userId}: {ex.Message}");
                        }
                    }
                }
                // ? ELSE rimosso: se userIds è vuota, _assignedUsers resta vuota (corretto!)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");
                // ? In caso di errore, assicurati che la lista sia vuota
                _assignedUsers.Clear();
            }
            finally
            {
                _isLoadingUsers = false;
                // ? IMPORTANTE: Forza sempre il render finale
                await InvokeAsync(StateHasChanged);
            }
        }

        protected void Edit()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Tickets/Edit/{Id}");
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
