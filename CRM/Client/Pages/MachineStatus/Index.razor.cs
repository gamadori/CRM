using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components;

namespace CRM.Client.Pages.MachineStatus
{
    /// <summary>
    /// La scheda "Elettronica e produzione" di una macchina: che versioni ha adesso, cosa e'
    /// cambiato e quanto ha lavorato.
    /// <para>
    /// Si legge e basta. Questi dati li scrive la macchina mandando la sua fotografia, non un
    /// operatore: correggerli a mano vorrebbe dire raccontare una versione che sull'impianto non
    /// c'e'.
    /// </para>
    /// </summary>
    public partial class Index : ComponentBase
    {
        [Inject] private HttpClient Http { get; set; } = default!;

        /// <summary>La macchina di cui mostrare lo stato.</summary>
        [Parameter] public int Id { get; set; }

        /// <summary>Serve solo nel messaggio di "non ha ancora mandato niente": e' cio' che va impostato sulla macchina.</summary>
        [Parameter] public string? SerialNumber { get; set; }

        private MachineStatusOverviewDTO? _data;

        private bool _loading = true;

        private int _days = 30;

        private readonly List<PeriodoOption> _periodi = new()
        {
            new("Ultimi 7 giorni", 7),
            new("Ultimi 30 giorni", 30),
            new("Ultimi 90 giorni", 90),
            new("Ultimo anno", 365)
        };

        protected override async Task OnParametersSetAsync() => await LoadAsync();

        private async Task CambiaPeriodo(int giorni)
        {
            _days = giorni;
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (Id <= 0)
                return;

            _loading = true;

            try
            {
                _data = await Http.GetFromJsonAsync<MachineStatusOverviewDTO>($"api/MachineStatus/{Id}?days={_days}");
            }
            catch (Exception)
            {
                _data = null;
            }
            finally
            {
                _loading = false;
            }
        }

        private static string Tipo(MachineComponentKind kind) => kind switch
        {
            MachineComponentKind.Hmi => "HMI",
            MachineComponentKind.Plc => "PLC",
            MachineComponentKind.Board => "Scheda",
            _ => "Altro"
        };

        /// <summary>
        /// Da quanto non si fa sentire. Oltre una settimana la versione mostrata non e' piu' lo
        /// stato della macchina ma l'ultima cosa che si sa, e va detto.
        /// </summary>
        private static bool Vecchio(DateTime lastSeen) => (DateTime.UtcNow - lastSeen).TotalDays > 7;

        private static string Quando(DateTime lastSeen)
        {
            var giorni = (int)(DateTime.UtcNow.Date - lastSeen.ToUniversalTime().Date).TotalDays;

            return giorni switch
            {
                <= 0 => "oggi",
                1 => "ieri",
                < 30 => $"{giorni} giorni fa",
                _ => lastSeen.ToLocalTime().ToString("dd/MM/yyyy")
            };
        }

        private sealed record PeriodoOption(string Text, int Value);
    }
}
