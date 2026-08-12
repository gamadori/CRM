using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Settings.SignatureTypes
{
    /// <summary>
    /// Che firma serve per ogni tipo di intervento.
    /// <para>
    /// Prima non c'era niente da configurare: la firma remota valeva per tutto cio' che non era
    /// Sul Posto o Ufficio, quindi anche per il lavoro in officina, e l'unico interruttore era
    /// generale.
    /// </para>
    /// </summary>
    public partial class Index
    {
        [Inject] private HttpClient Http { get; set; } = default!;

        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        [Inject] private IAGRestClientService RestClientServer { get; set; } = default!;

        [Inject] private NotificationService NotificationService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;

        private List<SupportTypeSetting> _settings = new();

        /// <summary>Serve solo per avvisare: con il canale remoto spento meta' delle scelte non ha effetto.</summary>
        private bool _remoteEnabled;

        private bool _loading = true;

        private bool _saving;

        private readonly List<RequirementOption> _requirements = new()
        {
            new(SignatureRequirement.None, "Nessuna firma"),
            new(SignatureRequirement.OnDevice, "Firma sul dispositivo"),
            new(SignatureRequirement.Remote, "Firma remota")
        };

        private sealed record RequirementOption(SignatureRequirement Value, string Text);

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

            try
            {
                _settings = await Http.GetFromJsonAsync<List<SupportTypeSetting>>("api/SupportTypeSettings")
                            ?? new List<SupportTypeSetting>();

                var globali = await RestClientServer.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath);
                _remoteEnabled = globali?.RemoteSignatureEnabled ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore caricamento impostazioni firma: {ex.Message}");
                Notify("Impossibile caricare le impostazioni della firma.", NotificationSeverity.Error);
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task SaveAsync()
        {
            _saving = true;

            try
            {
                var response = await Http.PostAsJsonAsync("api/SupportTypeSettings", _settings);

                if (response.IsSuccessStatusCode)
                    Notify("Impostazioni salvate.", NotificationSeverity.Success);
                else
                    Notify("Salvataggio non riuscito.", NotificationSeverity.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore salvataggio impostazioni firma: {ex.Message}");
                Notify($"Salvataggio non riuscito: {ex.Message}", NotificationSeverity.Error);
            }
            finally
            {
                _saving = false;
            }
        }

        private static string Explain(SignatureRequirement requirement) => requirement switch
        {
            SignatureRequirement.OnDevice => "Il cliente firma sullo schermo del tecnico e conferma con un codice. Se se n'è andato, resta il ripiego della firma da remoto.",
            SignatureRequirement.Remote => "Al cliente arriva un link via SMS o email e firma per conto suo.",
            _ => "Il verbale non chiede la firma e non la segnala mancante."
        };

        private void Notify(string message, NotificationSeverity severity)
            => NotificationService?.Notify(new NotificationMessage { Detail = message, Severity = severity });
    }
}
