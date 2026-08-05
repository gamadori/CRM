using CRM.Mobile.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace CRM.Mobile.Services;

/// <summary>
/// Il collegamento col CRM. Una chiave nell'intestazione <c>X-Api-Key</c>, nessun login
/// interattivo: allo stand non c'e' il tempo, e spesso nemmeno la rete per farlo.
/// </summary>
public sealed class CrmApiClient
{
    /// <summary>
    /// Timeout corto di proposito: sulla rete di una fiera una richiesta che non parte va
    /// abbandonata in fretta e rimessa in coda, non lasciata appesa mentre la persona aspetta.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

    private readonly AppSettingsStore _settingsStore;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public CrmApiClient(AppSettingsStore settingsStore) => _settingsStore = settingsStore;

    /// <summary>Vero quando il dispositivo dichiara una rete utilizzabile.</summary>
    public static bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>Verifica configurazione: dice se URL e chiave funzionano e a nome di chi si scrive.</summary>
    public async Task<(bool Ok, string Message)> PingAsync()
    {
        try
        {
            using var client = await CreateClientAsync();
            using var response = await client.GetAsync("api/field/ping");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Chiave rifiutata dal CRM: controlla di averla copiata per intero.");

            if (!response.IsSuccessStatusCode)
                return (false, $"Il CRM ha risposto {(int)response.StatusCode}. Controlla l'indirizzo.");

            var ping = await response.Content.ReadFromJsonAsync<FieldPingResponse>(_json);
            if (ping is not { Ok: true })
                return (false, "Risposta non valida dal CRM.");

            var scadenza = ping.ExpiresAt == null ? string.Empty : $" · chiave valida fino al {ping.ExpiresAt:dd/MM/yyyy}";
            return (true, $"Collegato come {ping.UserName}{scadenza}");
        }
        catch (InvalidOperationException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, await DescribeFailureAsync(ex));
        }
    }

    /// <summary>
    /// Spiega un errore di collegamento invece di riportarlo e basta.
    /// <para>
    /// "Connection failure" da solo manda a cercare nel posto sbagliato, mentre le due cause vere
    /// sono quasi sempre queste due, e si riconoscono dall'indirizzo: <c>localhost</c> scritto su un
    /// telefono indica il telefono stesso, e <c>https</c> verso un indirizzo di rete inciampa nel
    /// certificato di sviluppo, che e' emesso per "localhost".
    /// </para>
    /// </summary>
    private async Task<string> DescribeFailureAsync(Exception ex)
    {
        var settings = await _settingsStore.LoadAsync();

        if (!Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out var uri))
            return $"Nessuna risposta: {ex.Message}";

        var host = uri.Host;

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1")
        {
            return "Non raggiungibile: «localhost» qui indica questo dispositivo, non il computer "
                 + "dove gira il CRM. Usa l'indirizzo di rete del server (per esempio "
                 + "http://192.168.0.106:5000), oppure http://10.0.2.2:5000 se sei nell'emulatore Android.";
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return "Nessuna risposta in HTTPS. Il certificato di sviluppo del CRM vale solo per "
                 + $"«localhost», quindi su {host} viene rifiutato: prova con http:// sulla porta 5000. "
                 + $"({ex.Message})";
        }

        return $"Nessuna risposta da {host}: controlla che il CRM sia avviato e raggiungibile "
             + $"dalla stessa rete. ({ex.Message})";
    }

    public async Task<List<FieldInitiative>> GetInitiativesAsync()
    {
        using var client = await CreateClientAsync();
        return await client.GetFromJsonAsync<List<FieldInitiative>>("api/field/initiatives", _json)
               ?? new List<FieldInitiative>();
    }

    /// <summary>
    /// Legge il biglietto e restituisce i campi. Non solleva per un OCR fallito: e' una comodita',
    /// e la foto resta comunque allegata al lead.
    /// </summary>
    public async Task<BusinessCardExtractionResult> AnalyzeBusinessCardAsync(string path, string fileName)
    {
        try
        {
            using var client = await CreateClientAsync();
            using var content = await BuildPhotoContentAsync(path, fileName, "file");
            using var response = await client.PostAsync("api/field/cards/analyze", content);

            if (!response.IsSuccessStatusCode)
                return new BusinessCardExtractionResult { Success = false, ErrorMessage = "Lettura non riuscita." };

            return await response.Content.ReadFromJsonAsync<BusinessCardExtractionResult>(_json)
                   ?? new BusinessCardExtractionResult { Success = false, ErrorMessage = "Risposta vuota." };
        }
        catch (Exception ex)
        {
            return new BusinessCardExtractionResult { Success = false, ErrorMessage = $"Lettura non disponibile: {ex.Message}" };
        }
    }

    /// <summary>
    /// Invia un biglietto: dati e foto in <b>una</b> richiesta.
    /// <para>
    /// Non due chiamate (carica la foto, poi crea il lead): da un telefono in fiera la seconda
    /// fallisce spesso, e resterebbe un allegato orfano o un contatto senza la sua fonte.
    /// </para>
    /// </summary>
    public async Task<(bool Ok, string? Error)> SendLeadAsync(PendingLead pending)
    {
        try
        {
            using var client = await CreateClientAsync();
            using var content = new MultipartFormDataContent();

            var request = new FieldLeadRequest
            {
                IdInitiative = pending.InitiativeId,
                Name = pending.Name,
                CompanyName = pending.CompanyName,
                JobTitle = pending.JobTitle,
                Email = pending.Email,
                Phone = pending.Phone,
                Note = pending.Note,
                Score = pending.Score,
                CapturedAt = pending.CreatedAt,
                ClientId = pending.Id,
                AutoFillFromCard = pending.NeedsOcr
            };

            content.Add(new StringContent(JsonSerializer.Serialize(request, _json), Encoding.UTF8), "lead");

            if (!string.IsNullOrWhiteSpace(pending.PhotoPath) && File.Exists(pending.PhotoPath))
            {
                var photo = await BuildPhotoPartAsync(pending.PhotoPath, pending.PhotoFileName);
                content.Add(photo.Content, "photo", photo.FileName);
            }

            using var response = await client.PostAsync("api/field/leads", content);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Chiave rifiutata o scaduta.");

            if (!response.IsSuccessStatusCode)
                return (false, $"Il CRM ha risposto {(int)response.StatusCode}.");

            var result = await response.Content.ReadFromJsonAsync<FieldLeadResponse>(_json);

            // Anche un doppione e' un successo: significa che il biglietto e' gia' al sicuro nel
            // CRM, quindi va tolto dalla coda invece di essere ritentato per sempre.
            return result is { Ok: true } ? (true, null) : (false, result?.Message ?? "Risposta non valida.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<HttpClient> CreateClientAsync()
    {
        var settings = await _settingsStore.LoadAsync();

        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
            throw new InvalidOperationException("Manca l'indirizzo del CRM: impostalo in Configurazione.");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("Manca la chiave: impostala in Configurazione.");

        var baseUrl = settings.ApiBaseUrl.EndsWith('/') ? settings.ApiBaseUrl : settings.ApiBaseUrl + "/";
        var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = Timeout };
        client.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
        return client;
    }

    private static async Task<MultipartFormDataContent> BuildPhotoContentAsync(string path, string fileName, string partName)
    {
        var part = await BuildPhotoPartAsync(path, fileName);
        var content = new MultipartFormDataContent();
        content.Add(part.Content, partName, part.FileName);
        return content;
    }

    private static async Task<(ByteArrayContent Content, string FileName)> BuildPhotoPartAsync(string path, string? fileName)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(GuessContentType(path));

        return (part, string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(path) : fileName);
    }

    private static string GuessContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".heic" => "image/heic",
        ".heif" => "image/heif",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
