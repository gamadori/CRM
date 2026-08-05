using CRM.Mobile.Models;

namespace CRM.Mobile.Services;

/// <summary>
/// Dove vive la configurazione. URL e ultima fiera nelle preferenze normali; la chiave
/// nell'archivio protetto del dispositivo, perche' vale come credenziale - con quella si scrivono
/// lead a nome di una persona, e un telefono si perde.
/// </summary>
public sealed class AppSettingsStore
{
    private const string ApiBaseUrlKey = "crm_api_base_url";
    private const string ApiKeyKey = "crm_api_key";
    private const string LastInitiativeKey = "crm_last_initiative";

    private AppSettings? _cache;

    public async Task<AppSettings> LoadAsync()
    {
        if (_cache != null)
            return _cache;

        string apiKey;
        try
        {
            apiKey = await SecureStorage.Default.GetAsync(ApiKeyKey) ?? string.Empty;
        }
        catch (Exception ex)
        {
            // Su alcuni dispositivi l'archivio protetto non e' disponibile: si riparte da vuoto,
            // l'app chiede di reinserire la chiave invece di non aprirsi.
            Console.WriteLine($"Chiave non leggibile: {ex.Message}");
            apiKey = string.Empty;
        }

        _cache = new AppSettings
        {
            ApiBaseUrl = Preferences.Default.Get(ApiBaseUrlKey, string.Empty),
            ApiKey = apiKey,
            LastInitiativeId = Preferences.Default.Get(LastInitiativeKey, 0)
        };

        return _cache;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var url = settings.ApiBaseUrl.Trim();
        Preferences.Default.Set(ApiBaseUrlKey, url);
        Preferences.Default.Set(LastInitiativeKey, settings.LastInitiativeId);

        var apiKey = settings.ApiKey.Trim();
        if (string.IsNullOrEmpty(apiKey))
            SecureStorage.Default.Remove(ApiKeyKey);
        else
            await SecureStorage.Default.SetAsync(ApiKeyKey, apiKey);

        _cache = new AppSettings { ApiBaseUrl = url, ApiKey = apiKey, LastInitiativeId = settings.LastInitiativeId };
    }

    /// <summary>Ricorda l'ultima fiera scelta senza toccare il resto della configurazione.</summary>
    public async Task RememberInitiativeAsync(int idInitiative)
    {
        var settings = await LoadAsync();
        settings.LastInitiativeId = idInitiative;
        Preferences.Default.Set(LastInitiativeKey, idInitiative);
        _cache = settings;
    }
}
