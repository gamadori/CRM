using CRM.Mobile.Models;
using System.Text.Json;

namespace CRM.Mobile.Services;

/// <summary>
/// L'elenco delle fiere fra cui scegliere, con una copia locale.
/// <para>
/// La copia non e' un'ottimizzazione: allo stand la rete spesso non c'e', e senza elenco non si
/// potrebbe nemmeno dire a quale fiera appartiene il biglietto che si ha in mano. Si aggiorna
/// quando la rete c'e' e si usa comunque quando manca.
/// </para>
/// </summary>
public sealed class InitiativeCatalog
{
    private readonly CrmApiClient _api;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "initiatives.json");

    public InitiativeCatalog(CrmApiClient api) => _api = api;

    /// <summary>Quando l'elenco locale è stato aggiornato l'ultima volta; null se non c'è.</summary>
    public DateTime? CachedAt => File.Exists(_filePath) ? File.GetLastWriteTime(_filePath) : null;

    /// <summary>
    /// Elenco aggiornato se possibile, altrimenti l'ultimo salvato. Restituisce anche se i dati
    /// arrivano dalla copia locale, perche' l'interfaccia deve poterlo dire.
    /// </summary>
    public async Task<(List<FieldInitiative> Items, bool FromCache, string? Error)> GetAsync(bool forceRefresh = false)
    {
        var cached = await ReadCacheAsync();

        if (!forceRefresh && cached.Count > 0 && !CrmApiClient.IsOnline)
            return (cached, true, null);

        if (!CrmApiClient.IsOnline)
            return (cached, cached.Count > 0, "Nessuna rete: elenco non aggiornato.");

        try
        {
            var fresh = await _api.GetInitiativesAsync();
            if (fresh.Count > 0)
                await WriteCacheAsync(fresh);

            return (fresh.Count > 0 ? fresh : cached, fresh.Count == 0 && cached.Count > 0, null);
        }
        catch (Exception ex)
        {
            // Un errore di rete non deve svuotare la tendina: si continua con l'ultimo elenco noto.
            return (cached, cached.Count > 0, cached.Count > 0
                ? "Elenco non aggiornato: uso l'ultimo scaricato."
                : $"Elenco non disponibile: {ex.Message}");
        }
    }

    private async Task<List<FieldInitiative>> ReadCacheAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new List<FieldInitiative>();

            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<FieldInitiative>>(stream, _json) ?? new List<FieldInitiative>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Elenco fiere locale illeggibile: {ex.Message}");
            return new List<FieldInitiative>();
        }
    }

    private async Task WriteCacheAsync(List<FieldInitiative> items)
    {
        try
        {
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, items, _json);
        }
        catch (Exception ex)
        {
            // Senza copia locale l'app funziona lo stesso finche' c'e' rete: non vale un errore.
            Console.WriteLine($"Elenco fiere non salvato in locale: {ex.Message}");
        }
    }
}
