using CRM.Mobile.Models;

namespace CRM.Mobile.Services;

/// <summary>
/// Svuota la coda verso il CRM, uno alla volta e in ordine di raccolta.
/// <para>
/// Sta in un servizio suo e non nella pagina perche' deve poter partire anche quando la pagina
/// non e' aperta: al ritorno della rete, all'avvio, dopo ogni salvataggio. Un biglietto che
/// aspetta un'apertura di schermata per partire e' un biglietto che qualcuno dimentica.
/// </para>
/// </summary>
public sealed class LeadSyncService
{
    private readonly LeadQueueStore _queue;
    private readonly CrmApiClient _api;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LeadSyncService(LeadQueueStore queue, CrmApiClient api)
    {
        _queue = queue;
        _api = api;

        // Il ritorno della rete e' il momento buono per riprovare: interrogare a vuoto ogni pochi
        // secondi, con la batteria di un telefono in fiera, sarebbe peggio del problema.
        Connectivity.Current.ConnectivityChanged += async (_, args) =>
        {
            if (args.NetworkAccess == NetworkAccess.Internet)
                await FlushAsync();
        };
    }

    /// <summary>Segnalato dopo ogni svuotamento, cosi' le pagine aperte aggiornano i contatori.</summary>
    public event EventHandler<SyncResult>? Synced;

    public async Task<SyncResult> FlushAsync()
    {
        if (!await _gate.WaitAsync(0))
            return new SyncResult(0, await _queue.CountAsync(), "Invio già in corso.");

        try
        {
            if (!CrmApiClient.IsOnline)
                return await ReportAsync(0, "Nessuna rete: i biglietti restano in coda.");

            var sent = 0;
            string? error = null;

            foreach (var lead in await _queue.ListAsync())
            {
                var (ok, failure) = await _api.SendLeadAsync(lead);
                if (!ok)
                {
                    // Ci si ferma al primo fallimento: se la rete non c'e', insistere sugli altri
                    // serve solo a consumare batteria. L'ordine di raccolta si conserva.
                    await _queue.MarkFailedAsync(lead.Id, failure);
                    error = failure;
                    break;
                }

                await _queue.RemoveAsync(lead.Id);
                sent++;
            }

            return await ReportAsync(sent, error);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SyncResult> ReportAsync(int sent, string? error)
    {
        var result = new SyncResult(sent, await _queue.CountAsync(), error);
        Synced?.Invoke(this, result);
        return result;
    }

    public sealed record SyncResult(int Sent, int Pending, string? Error);
}
