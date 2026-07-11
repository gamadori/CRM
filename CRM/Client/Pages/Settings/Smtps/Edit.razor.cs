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

namespace CRM.Client.Pages.Settings.Smtps
{
    public partial class Edit : ComponentBase
    {
        [Inject]
        ISmtpSettingsService SmtpSettingsService { get; set; } = default!;

        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        HttpClient Http { get; set; } = default!;

        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        [Parameter]
        public int? Id { get; set; }

        private SmtpSetting? _item;
        private bool _isLoading = true;
        private string? _testResult;
        private bool _testOk;
        private bool _testInProgress;

        private readonly List<EmailProvider> _providers = Enum.GetValues<EmailProvider>().ToList();

        private PageHeaderModel? _pageHeader;

        protected override async Task OnParametersSetAsync()
        {
            _isLoading = true;
            StateHasChanged();

            _item = Id != null
                ? await SmtpSettingsService.GetItemAsync(Id.Value) ?? new SmtpSetting()
                : new SmtpSetting();

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
                    await SmtpSettingsService.PostAsync(_item);
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                _isLoading = false;
                NavigationManager.NavigateTo("/Settings/Smtps");
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings/Smtps");
        }

        protected async Task TestAsync()
        {
            if (_item == null) return;
            _testResult = null;
            _testInProgress = true;
            StateHasChanged();
            try
            {
                var response = await Http.PostAsJsonAsync("api/SmtpSettings/Test", _item);
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
