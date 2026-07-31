using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets
{
    [Authorize]
    public partial class Preview: ComponentBase
    {
        [Inject]
        private ITicketsService _service { get; set; }

        [Inject]
        private IBaseRestService<ApplicationUser, UsersFilterModel, string> _serviceUsers { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        private HttpClient HttpClient { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public object IdTicket { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public bool ViewCommands { get; set; } = true;

        private Ticket _ticket = null;

        private ApplicationUser _userOpened = null;

        private ApplicationUser _userAssigned = null;

        // ✅ NUOVO: Lista di tutti gli utenti assegnati
        private List<ApplicationUser> _assignedUsers = new List<ApplicationUser>();

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
            // Lo stato del ticket precedente va azzerato PRIMA di caricare. Questo componente e'
            // riusato: nello scheduler l'offcanvas dei dettagli sta sempre nel DOM e cambia solo il
            // parametro IdTicket, quindi Blazor mantiene la stessa istanza e OnInitializedAsync non
            // riparte. Senza reset, un ticket che non ha utenti assegnati non sovrascrive nulla e
            // continuava a mostrare gli assegnatari del ticket aperto prima.
            ResetState();

            try
            {

                if (Id != null)
                {

                    _ticket = await _service.Get(Id.Value);
                    _userOpened = await GetUserOrNull(_ticket.IdUserOpened);

                    // ⚠️ LEGACY: Mantieni per retrocompatibilità
                    _userAssigned = await GetUserOrNull(_ticket.IdUserAssigned);

                    // ✅ NUOVO: Carica tutti gli utenti assegnati
                    await LoadAssignedUsers();
                }
                else
                    _ticket = new Ticket();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>Azzera tutto cio' che appartiene al ticket mostrato finora.</summary>
        private void ResetState()
        {
            _ticket = null;
            _userOpened = null;
            _userAssigned = null;
            _assignedUsers.Clear();
        }

        /// <summary>
        /// Un id utente vuoto non deve diventare una chiamata: fallirebbe, e l'eccezione
        /// interromperebbe il caricamento lasciando a video i dati del ticket precedente.
        /// </summary>
        private async Task<ApplicationUser> GetUserOrNull(string idUser)
            => string.IsNullOrWhiteSpace(idUser) ? null : await _serviceUsers.Get(idUser);

        /// <summary>
        /// ✅ NUOVO: Carica tutti gli utenti assegnati al ticket dalla tabella TicketUserAssignments
        /// </summary>
        private async Task LoadAssignedUsers()
        {
            try
            {
                if (_ticket == null || _ticket.Id == 0)
                    return;

                // La lista arriva gia' vuota da ResetState: qui si riempie e basta. I Clear() che
                // c'erano stavano dentro i rami che TROVAVANO qualcosa, quindi il caso "nessun
                // utente assegnato" non ripuliva niente ed e' esattamente quello che lasciava a
                // video gli assegnatari del ticket precedente.
                var userIds = await HttpClient.GetFromJsonAsync<List<string>>($"api/Tickets/{_ticket.Id}/assigned-users");

                if (userIds != null && userIds.Any())
                {
                    foreach (var userId in userIds)
                    {
                        var user = await GetUserOrNull(userId);
                        if (user != null)
                        {
                            _assignedUsers.Add(user);
                        }
                    }
                }
                else if (_userAssigned != null)
                {
                    // Fallback sul campo legacy solo se la tabella delle assegnazioni e' vuota.
                    _assignedUsers.Add(_userAssigned);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento utenti assegnati: {ex.Message}");

                // Fallback in caso di errore, senza reintrodurre dati di un altro ticket.
                if (!_assignedUsers.Any() && _userAssigned != null)
                {
                    _assignedUsers.Add(_userAssigned);
                }
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
                NavigationManager.NavigateTo("/Tickets/Index");
        }

        protected void SendInvitation()
        {

        }

        /// <summary>
        /// ✅ NUOVO: Estrae le iniziali dal nome completo dell'utente
        /// </summary>
        private string GetUserInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return "?";

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            else if (parts.Length == 1 && parts[0].Length >= 2)
            {
                return parts[0].Substring(0, 2).ToUpper();
            }
            else if (parts.Length == 1)
            {
                return parts[0][0].ToString().ToUpper();
            }

            return "?";
        }

        private static string GetContrastTextColor(string backgroundColor)
        {
            if (string.IsNullOrWhiteSpace(backgroundColor))
                return "#ffffff";

            var hex = backgroundColor.Trim().TrimStart('#');
            if (hex.Length == 3)
                hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

            if (hex.Length != 6)
                return "#ffffff";

            try
            {
                var red = Convert.ToInt32(hex.Substring(0, 2), 16);
                var green = Convert.ToInt32(hex.Substring(2, 2), 16);
                var blue = Convert.ToInt32(hex.Substring(4, 2), 16);
                var luminance = (0.299 * red + 0.587 * green + 0.114 * blue) / 255;

                return luminance > 0.55 ? "#17212b" : "#ffffff";
            }
            catch
            {
                return "#ffffff";
            }
        }

        private static string GetContrastBackgroundColor(string backgroundColor)
        {
            return string.IsNullOrWhiteSpace(backgroundColor) ? "#5b6570" : backgroundColor;
        }

    }
}
