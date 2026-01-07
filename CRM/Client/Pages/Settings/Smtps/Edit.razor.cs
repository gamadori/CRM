using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;



namespace CRM.Client.Pages.Settings.Smtps
{
    public partial class Edit: ComponentBase
    {
        [Inject]
        ISmtpSettingsService SmtpSettingsService { get; set; } = default!;
        
       
        
        [Inject]
        NavigationManager NavigationManager { get; set; } = default!;
        
        [Inject]
        HttpClient Http { get; set; } = default!;
        
        [Inject]
        IHeaderService HeaderService { get; set; } = default!;

        private SmtpSetting? _item = null;
        private bool _isLoading = true;
        private string? _smtpTestResult = null;
        private bool _smtpTestInProgress = false;

        private PageHeaderModel _pageHeader = new PageHeaderModel();

        protected override async Task OnInitializedAsync()
        {
            _isLoading = true;
            StateHasChanged();
            _item = await SmtpSettingsService.GetFirstAsync() ?? new SmtpSetting();
            _pageHeader = await HeaderService.Create();

            _isLoading = false;
            StateHasChanged();

        }
        private async Task SaveAsync()
        {
            if (_item != null)
            {
                var resp = await SmtpSettingsService.PostAsync(_item);
            }
        }

        protected async Task HandleValidSubmit()
        {
            bool resp;

            try
            {
                _isLoading = true;
                StateHasChanged();

               await SaveAsync();
                resp = true;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                _isLoading = false;
                NavigationManager.NavigateTo("/Settings");
            }
        }

        protected void Annulla()
        {
            NavigationManager.NavigateTo("/Settings");
        }

        protected async Task TestSmtpAsync()
        {
            if (_item == null) return;
            _smtpTestResult = null;
            _smtpTestInProgress = true;
            StateHasChanged();
            try
            {
                var response = await Http.PostAsJsonAsync("api/SmtpSettings/Test", _item);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<TestSmtpResult>();
                    _smtpTestResult = result?.Message ?? "Test completato.";
                }
                else
                {
                    var result = await response.Content.ReadFromJsonAsync<TestSmtpResult>();
                    _smtpTestResult = result?.Message ?? "Errore durante il test SMTP.";
                }
            }
            catch (System.Exception ex)
            {
                _smtpTestResult = $"Errore: {ex.Message}";
            }
            finally
            {
                _smtpTestInProgress = false;
                StateHasChanged();
            }
        }

        public class TestSmtpResult
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
        }
    }
}
