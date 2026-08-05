using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Radzen;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.Initiatives
{
    public partial class Edit : ComponentBase
    {
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IInitiativeService InitiativeService { get; set; } = default!;
        [Inject] private DialogService DialogService { get; set; } = default!;
        [Inject] private IHeaderService HeaderService { get; set; } = default!;
        [Inject] private IBaseRestService<ApplicationUser, UsersFilterModel, string> UserService { get; set; } = default!;
        [Inject] private HttpClient Http { get; set; } = default!;

        [Parameter] public int? Id { get; set; }

        [Parameter] public PageModality PageMode { get; set; } = PageModality.Visualization;

        [Parameter] public Func<Task>? OnClickSave { get; set; }

        [Parameter] public Action? OnClickCancel { get; set; }

        private PageHeaderModel? _pageHeader;
        private Initiative _model = new();
        private InitiativeState _originalState;
        private List<ApplicationUser> _users = new();
        private List<InitiativeMemberDTO> _members = new();
        private string? _memberToAdd;
        private bool _loading = true;
        private bool _saving;
        private string? _error;

        private bool IsNew => Id == null || Id == 0;

        /// <summary>Chi non e' ancora nell'elenco: la tendina di aggiunta non ripropone chi c'e' gia'.</summary>
        private List<ApplicationUser> _addableUsers = new();

        private static readonly List<RoleOption> RoleOptions = new()
        {
            new("Partecipante", InitiativeMemberRole.Participant),
            new("Commerciale", InitiativeMemberRole.Sales),
            new("Tecnico", InitiativeMemberRole.Technical),
            new("Stand", InitiativeMemberRole.Stand),
            new("Logistica", InitiativeMemberRole.Logistics),
            new("Altro", InitiativeMemberRole.Other)
        };

        private record RoleOption(string Text, InitiativeMemberRole Value);

        protected override async Task OnParametersSetAsync()
        {
            _pageHeader = await HeaderService.Create();
            _loading = true;
            await LoadUsersAsync();

            if (IsNew)
            {
                // Il tipo arriva dalla rotta di provenienza: chi clicca "Nuova trasferta" dalla
                // lista delle trasferte non deve ridirlo in un menu a tendina.
                var kind = InitiativeKind.Trip;
                if (Uri.TryCreate(NavigationManager.Uri, UriKind.Absolute, out var uri)
                    && QueryHelpers.ParseQuery(uri.Query).TryGetValue("kind", out var raw)
                    && Enum.TryParse<InitiativeKind>(raw.FirstOrDefault(), ignoreCase: true, out var parsed))
                {
                    kind = parsed;
                }

                _model = new Initiative
                {
                    Kind = kind,
                    State = InitiativeState.Planned,
                    DateFrom = DateTime.Today,
                    DateTo = DateTime.Today
                };

                _members = new List<InitiativeMemberDTO>();

                // Chi organizza di solito ci va: si parte con il responsabile gia' nell'elenco,
                // togliibile per il caso - reale ma meno frequente - di chi organizza e non parte.
                var me = await SafeCurrentUserIdAsync();
                if (!string.IsNullOrWhiteSpace(me))
                {
                    _model.IdOwner = me;
                    AddMember(me);
                }
            }
            else
            {
                var dto = await InitiativeService.GetItemAsync(Id!.Value);
                _model = dto?.ToEntity() ?? new Initiative { DateFrom = DateTime.Today, DateTo = DateTime.Today };
                _members = dto?.Members.Select(m => new InitiativeMemberDTO
                {
                    Id = m.Id,
                    IdInitiative = m.IdInitiative,
                    IdUser = m.IdUser,
                    UserName = m.UserName,
                    Role = m.Role,
                    Notes = m.Notes,
                    AddedAt = m.AddedAt
                }).ToList() ?? new List<InitiativeMemberDTO>();
            }

            RefreshAddableUsers();
            _originalState = _model.State;
            _loading = false;
        }

        private void AddMember(string? idUser)
        {
            _memberToAdd = null;

            if (string.IsNullOrWhiteSpace(idUser) || _members.Any(m => m.IdUser == idUser))
                return;

            var user = _users.FirstOrDefault(u => u.Id == idUser);
            if (user == null)
                return;

            _members.Add(new InitiativeMemberDTO
            {
                IdUser = user.Id,
                UserName = user.NameComplete,
                Role = InitiativeMemberRole.Participant
            });

            RefreshAddableUsers();
        }

        private void RemoveMember(InitiativeMemberDTO member)
        {
            _members.Remove(member);
            RefreshAddableUsers();
        }

        private void RefreshAddableUsers()
        {
            var taken = _members.Select(m => m.IdUser).ToHashSet(StringComparer.Ordinal);
            _addableUsers = _users.Where(u => !taken.Contains(u.Id)).ToList();
        }

        private async Task<string?> SafeCurrentUserIdAsync()
        {
            try
            {
                var me = await Http.GetFromJsonAsync<ApplicationUser>("api/Users/CurrentUser");
                return me?.Id;
            }
            catch (Exception ex)
            {
                // Senza utente corrente si parte con l'elenco vuoto: si perde una comodita', non
                // la possibilita' di comporre la squadra.
                Console.WriteLine($"Utente corrente non determinato: {ex.Message}");
                return null;
            }
        }

        private async Task LoadUsersAsync()
        {
            var response = await UserService.GetList(new UsersFilterModel());
            _users = response?.Items?.OrderBy(x => x.NameComplete).ToList() ?? new List<ApplicationUser>();
        }

        private async Task Save()
        {
            _error = null;

            if (string.IsNullOrWhiteSpace(_model.Name))
            {
                _error = "Il nome e' obbligatorio.";
                return;
            }

            if (_model.DateTo < _model.DateFrom)
            {
                _error = "La data di fine e' precedente a quella di inizio.";
                return;
            }

            if ((_model.State == InitiativeState.Closed || _model.State == InitiativeState.Cancelled)
                && _originalState != _model.State)
            {
                var message = _model.State == InitiativeState.Closed
                    ? "Chiudere questa iniziativa? Dopo il salvataggio verra' trattata come consuntivata."
                    : "Annullare questa iniziativa? Le presenze e i dati collegati resteranno storicizzati.";

                if (await DialogService.Confirm(message, "Conferma cambio stato") != true)
                    return;
            }

            _saving = true;
            try
            {
                // Si manda il ruolo scelto, non un valore di comodo: il server prende questo come
                // verita' e sovrascriverlo con "Partecipante" cancellerebbe in silenzio le scelte
                // fatte qui.
                _model.Members = _members
                    .Where(m => !string.IsNullOrWhiteSpace(m.IdUser))
                    .Select(m => new InitiativeMember
                    {
                        IdUser = m.IdUser,
                        Role = m.Role,
                        Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim()
                    })
                    .ToList();

                var response = await InitiativeService.PostAsync(_model);
                if (!response.State)
                {
                    _error = response.Message;
                    return;
                }

                if (PageMode == PageModality.Child && OnClickSave != null)
                {
                    await OnClickSave.Invoke();
                }
                else
                {
                    var id = response.Data?.Id ?? _model.Id;
                    NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/{id}/Info");
                }
            }
            finally
            {
                _saving = false;
            }
        }

        private void Cancel()
        {
            if (PageMode == PageModality.Child && OnClickCancel != null)
            {
                OnClickCancel.Invoke();
                return;
            }

            if (IsNew)
                NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/List/{_model.Kind}");
            else
                NavigationManager.NavigateTo($"/{ConstHelper.ClientInitiativesPath}/{Id}/Info");
        }

        private static string KindText(InitiativeKind kind) => Index.KindText(kind);

        private static string StateText(InitiativeState state) => Index.StateText(state);
    }
}
