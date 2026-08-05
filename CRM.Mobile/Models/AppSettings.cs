namespace CRM.Mobile.Models;

/// <summary>
/// Configurazione del collegamento al CRM. Sta in una pagina sua, separata dalla cattura: si
/// imposta una volta prima della fiera e allo stand non si tocca piu'.
/// </summary>
public sealed class AppSettings
{
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Chiave dell'app, generata dal CRM. Vale come credenziale: chi ce l'ha scrive lead a nome
    /// di una persona, quindi non finisce nelle preferenze in chiaro ma nell'archivio protetto
    /// del dispositivo.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Ultima fiera scelta: si ripropone al riavvio, cosi' non la si riseleziona ogni volta.</summary>
    public int LastInitiativeId { get; set; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(ApiBaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}
