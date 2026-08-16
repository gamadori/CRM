using System;
using System.Threading.Tasks;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace CRM.Client.Pages.Settings.MachineBackups
{
    /// <summary>
    /// Le impostazioni dei backup macchina, in una pagina loro.
    /// <para>
    /// Stavano in Impostazioni generali, che e' gia' una pagina lunghissima: una regola che
    /// <b>cancella file dei clienti</b> non puo' abitare in fondo a un elenco dove nessuno la
    /// legge.
    /// </para>
    /// <para>
    /// I valori restano sull'unica riga di configurazione del programma: cambia dove si modificano,
    /// non dove sono scritti.
    /// </para>
    /// </summary>
    public partial class Index : ComponentBase
    {
        [Inject] private IAGRestClientService RestClientService { get; set; } = default!;

        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        [Inject] private NotificationService NotificationService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;

        private GlobalSetting? _settings;

        private bool _loading = true;

        private bool _saving;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

            if (_pageHeader != null)
            {
                _pageHeader.Title = "Backup delle macchine";
                _pageHeader.Subtitle = "Quante versioni tenere e quando segnalare una macchina che tace";
                _pageHeader.Icon = "backup";
            }

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _loading = true;

            try
            {
                // Si legge e si riscrive l'impostazione INTERA: salvando solo i campi di questa
                // pagina si azzererebbe tutto il resto della configurazione.
                _settings = await RestClientService.GetFirst<GlobalSetting>(ConstHelper.GlobalSettingsPath)
                            ?? new GlobalSetting();
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Caricamento", ex.Message);
                _settings ??= new GlobalSetting();
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task SaveAsync()
        {
            if (_settings == null || _saving)
                return;

            _saving = true;

            try
            {
                var resp = await RestClientService.Post<GlobalSetting, int>(_settings, ConstHelper.GlobalSettingsPath);

                if (resp == null || !resp.State)
                {
                    Notify(NotificationSeverity.Error, "Backup macchine", "Salvataggio non riuscito");
                    return;
                }

                Notify(NotificationSeverity.Success, "Backup macchine", "Impostazioni salvate");
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Backup macchine", ex.Message);
            }
            finally
            {
                _saving = false;
            }
        }

        private void Notify(NotificationSeverity severity, string summary, string detail) =>
            NotificationService?.Notify(severity, summary, detail);
    }
}
