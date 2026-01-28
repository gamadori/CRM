using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Radzen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Pages.TicketInterventions
{
    public partial class LanguageSelectionDialog : ComponentBase
    {
        [Inject]
        private ILanguagesService LanguagesService { get; set; } = default!;

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; } = default!;

        [Inject]
        private DialogService DialogService { get; set; } = default!;

        [Parameter]
        public string? DefaultLanguageCode { get; set; }

        private List<Language> _languages = new();
        private string? _selectedLanguageCode;
        private bool _loading = true;

        protected override async Task OnInitializedAsync()
        {
            await LoadLanguages();
        }

        private async Task LoadLanguages()
        {
            _loading = true;
            
            try
            {
                _languages = await LanguagesService.GetListAsync(null) ?? new List<Language>();
                
                // Imposta lingua di default (utente corrente o cultura corrente)
                if (!string.IsNullOrEmpty(DefaultLanguageCode))
                {
                    _selectedLanguageCode = DefaultLanguageCode;
                }
                else
                {
                    var userLanguageCode = await LanguagesService.GetCodeLanguage();
                    _selectedLanguageCode = userLanguageCode ?? CultureInfo.CurrentCulture.Name;
                }

                // Verifica che la lingua selezionata esista nella lista
                if (_languages.Any() && !_languages.Any(l => l.LanguageCode == _selectedLanguageCode))
                {
                    _selectedLanguageCode = _languages.FirstOrDefault()?.LanguageCode;
                }
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"Error loading languages: {ex.Message}");
                _languages = new List<Language>();
            }
            finally
            {
                _loading = false;
            }
        }

        private void SelectLanguage(string languageCode)
        {
            _selectedLanguageCode = languageCode;
            StateHasChanged();
        }

        private void Confirm()
        {
            if (!string.IsNullOrEmpty(_selectedLanguageCode))
            {
                DialogService.Close(_selectedLanguageCode);
            }
        }

        private void Cancel()
        {
            DialogService.Close(null);
        }
    }
}
