using CRM.Mobile.Models;
using System.Text.Json;

namespace CRM.Mobile.Services;

public sealed class LeadQueueStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _filePath = Path.Combine(FileSystem.AppDataDirectory, "pending-leads.json");

    public async Task<IReadOnlyList<PendingLead>> ListAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadUnsafeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> CountAsync()
    {
        var items = await ListAsync();
        return items.Count;
    }

    public async Task EnqueueAsync(PendingLead lead)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await ReadUnsafeAsync();
            items.Add(lead);
            await WriteUnsafeAsync(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Registra un invio fallito senza toglierlo dalla coda: il conteggio dei tentativi e l'ultimo
    /// errore si mostrano, cosi' una coda che non parte smette di essere un mistero silenzioso.
    /// </summary>
    public async Task MarkFailedAsync(string id, string? error)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await ReadUnsafeAsync();
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item == null)
                return;

            item.Attempts++;
            item.LastError = error;
            await WriteUnsafeAsync(items);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string id)
    {
        await _gate.WaitAsync();
        try
        {
            var items = await ReadUnsafeAsync();
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item?.PhotoPath is { Length: > 0 } && File.Exists(item.PhotoPath))
                File.Delete(item.PhotoPath);

            await WriteUnsafeAsync(items.Where(x => x.Id != id).ToList());
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<PendingLead>> ReadUnsafeAsync()
    {
        if (!File.Exists(_filePath))
            return new List<PendingLead>();

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<List<PendingLead>>(stream, _json) ?? new List<PendingLead>();
    }

    private async Task WriteUnsafeAsync(List<PendingLead> leads)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, leads, _json);
    }
}
