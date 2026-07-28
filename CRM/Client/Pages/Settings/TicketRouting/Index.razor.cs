using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace CRM.Client.Pages.Settings.TicketRouting
{
    /// <summary>
    /// Configurazione dello smistamento automatico dei ticket. La pagina tiene insieme le tre cose
    /// che servono per farlo funzionare davvero: i parametri, le competenze dei gruppi su cui l'AI
    /// decide e una prova a vuoto per verificare l'effetto prima di accendere l'automatismo.
    /// </summary>
    public partial class Index
    {
        [Inject] private HttpClient Http { get; set; } = default!;

        [Inject] private IHeaderService HeaderService { get; set; } = default!;

        [Inject] private NotificationService NotificationService { get; set; } = default!;

        private PageHeaderModel? _pageHeader;

        private TicketRoutingSetting? _settings;

        private TicketRoutingStatusDTO? _status;

        private List<TicketRoutingGroupDTO> _groups = new();

        private List<TicketType> _ticketTypes = new();

        private bool _loading = true;

        private bool _saving;

        /// <summary>Testo in modifica per ogni gruppo, per salvare una riga alla volta senza perdere le altre.</summary>
        private readonly Dictionary<int, string?> _hintsDraft = new();

        private int? _savingHintsGroupId;

        // ─── Prova di smistamento ───────────────────────────────────────────────
        private int? _previewTypeId;

        private string _previewText = string.Empty;

        private bool _previewRunning;

        private TicketRoutingPreviewResult? _previewResult;

        protected override async Task OnInitializedAsync()
        {
            _pageHeader = await HeaderService.Create();

            if (_pageHeader != null)
            {
                _pageHeader.Title = "Smistamento AI dei ticket";
                _pageHeader.Subtitle = "Assegna automaticamente i ticket in arrivo al gruppo competente";
                _pageHeader.Icon = "alt_route";
            }

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            _loading = true;

            try
            {
                _settings = await Http.GetFromJsonAsync<TicketRoutingSetting>("api/TicketRouting/settings") ?? new TicketRoutingSetting();
                _status = await Http.GetFromJsonAsync<TicketRoutingStatusDTO>("api/TicketRouting/status");
                _groups = await Http.GetFromJsonAsync<List<TicketRoutingGroupDTO>>("api/TicketRouting/groups") ?? new();
                _ticketTypes = await Http.GetFromJsonAsync<List<TicketType>>("api/TicketTypes") ?? new();

                _hintsDraft.Clear();
                foreach (var group in _groups)
                    _hintsDraft[group.Id] = group.AiRoutingHints;

                _previewTypeId ??= _ticketTypes.FirstOrDefault()?.Id;
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Caricamento", ex.Message);
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            if (_settings == null || _saving)
                return;

            _saving = true;

            try
            {
                var response = await Http.PutAsJsonAsync("api/TicketRouting/settings", _settings);

                if (!response.IsSuccessStatusCode)
                {
                    Notify(NotificationSeverity.Error, "Impostazioni", "Salvataggio non riuscito");
                    return;
                }

                _settings = await response.Content.ReadFromJsonAsync<TicketRoutingSetting>() ?? _settings;
                _status = await Http.GetFromJsonAsync<TicketRoutingStatusDTO>("api/TicketRouting/status");

                Notify(NotificationSeverity.Success, "Impostazioni", "Configurazione salvata");
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Impostazioni", ex.Message);
            }
            finally
            {
                _saving = false;
            }
        }

        private async Task SaveHintsAsync(TicketRoutingGroupDTO group)
        {
            if (_savingHintsGroupId != null)
                return;

            _savingHintsGroupId = group.Id;

            try
            {
                var request = new TicketRoutingHintsRequest { AiRoutingHints = _hintsDraft.GetValueOrDefault(group.Id) };
                var response = await Http.PutAsJsonAsync($"api/TicketRouting/groups/{group.Id}/hints", request);

                if (!response.IsSuccessStatusCode)
                {
                    Notify(NotificationSeverity.Error, "Competenze", "Salvataggio non riuscito");
                    return;
                }

                group.AiRoutingHints = request.AiRoutingHints;
                _status = await Http.GetFromJsonAsync<TicketRoutingStatusDTO>("api/TicketRouting/status");

                Notify(NotificationSeverity.Success, "Competenze", $"Competenze di {group.Name} aggiornate");
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Competenze", ex.Message);
            }
            finally
            {
                _savingHintsGroupId = null;
            }
        }

        private async Task RunPreviewAsync()
        {
            if (_previewRunning || _previewTypeId == null || string.IsNullOrWhiteSpace(_previewText))
                return;

            _previewRunning = true;
            _previewResult = null;

            try
            {
                var request = new TicketRoutingPreviewRequest
                {
                    IdTicketType = _previewTypeId.Value,
                    Description = _previewText.Trim()
                };

                var response = await Http.PostAsJsonAsync("api/TicketRouting/preview", request);

                if (!response.IsSuccessStatusCode)
                {
                    Notify(NotificationSeverity.Error, "Prova", "Prova non riuscita");
                    return;
                }

                _previewResult = await response.Content.ReadFromJsonAsync<TicketRoutingPreviewResult>();
            }
            catch (Exception ex)
            {
                Notify(NotificationSeverity.Error, "Prova", ex.Message);
            }
            finally
            {
                _previewRunning = false;
            }
        }

        private bool HintsChanged(TicketRoutingGroupDTO group) =>
            (_hintsDraft.GetValueOrDefault(group.Id) ?? string.Empty) != (group.AiRoutingHints ?? string.Empty);

        private static string Percent(double? value) =>
            value == null ? "—" : $"{Math.Round(value.Value * 100)}%";

        private void Notify(NotificationSeverity severity, string summary, string detail) =>
            NotificationService?.Notify(severity, summary, detail);
    }
}
