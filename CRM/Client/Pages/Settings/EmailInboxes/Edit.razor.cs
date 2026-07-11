using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings.EmailInboxes
{
    public partial class Edit : ComponentBase
    {
        [Inject]
        IEmailInboxService Service { get; set; } = default!;

        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        HttpClient Http { get; set; } = default!;

        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        [Inject]
        ITicketTypesService TicketTypesService { get; set; } = default!;

        [Inject]
        IBaseRestService<ApplicationUser, UsersFilterModel, string> UsersService { get; set; } = default!;

        [Parameter]
        public int? Id { get; set; }

        private EmailInbox? _item;

        private List<TicketType> _ticketTypes = new();
        private List<ApplicationUser> _users = new();
        private bool _isLoading = true;
        private string? _testResult;
        private bool _testOk;
        private bool _testInProgress;

        /// <summary>Voce di dropdown per un enum: testo leggibile (attributo Display) + valore.</summary>
        private sealed record EnumOption<T>(string Text, T Value);

        private static List<EnumOption<T>> Options<T>() where T : struct, Enum =>
            Enum.GetValues<T>().Select(v => new EnumOption<T>(UtilityHelper.GetDisplayName(v), v)).ToList();

        private readonly List<EnumOption<EmailInboxMode>> _modes = Options<EmailInboxMode>();
        private readonly List<EnumOption<InboundAction>> _actions = Options<InboundAction>();
        private readonly List<EnumOption<EmailProvider>> _providers = Options<EmailProvider>();

        private PageHeaderModel? _pageHeader;

        protected override async Task OnInitializedAsync()
        {
            var types = await TicketTypesService.GetList(new TicketTypeFilter());
            _ticketTypes = types?.Items?.ToList() ?? new();

            var users = await UsersService.GetList(new UsersFilterModel());
            _users = users?.Items?.ToList() ?? new();
        }

        protected override async Task OnParametersSetAsync()
        {
            _isLoading = true;
            StateHasChanged();

            _item = Id != null
                ? await Service.GetItemAsync(Id.Value) ?? new EmailInbox()
                : new EmailInbox();

            _pageHeader = await HeaderService.Create();

            _isLoading = false;
            StateHasChanged();
        }

        protected async Task HandleValidSubmit()
        {
            try
            {
                _isLoading = true;
                StateHasChanged();

                if (_item != null)
                    await Service.PostAsync(_item);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                _isLoading = false;
                NavigationManager.NavigateTo("/Settings/EmailInboxes");
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings/EmailInboxes");
        }

        protected async Task TestAsync()
        {
            if (_item == null) return;
            _testResult = null;
            _testInProgress = true;
            StateHasChanged();
            try
            {
                var response = await Http.PostAsJsonAsync("api/EmailInbox/Test", _item);
                var result = await response.Content.ReadFromJsonAsync<TestResult>();
                _testOk = response.IsSuccessStatusCode && (result?.Success ?? false);
                _testResult = result?.Message ?? (_testOk ? "Test completato." : "Errore durante il test.");
            }
            catch (Exception ex)
            {
                _testOk = false;
                _testResult = $"Errore: {ex.Message}";
            }
            finally
            {
                _testInProgress = false;
                StateHasChanged();
            }
        }

        public class TestResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}
