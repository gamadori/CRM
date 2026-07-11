namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Esito dell'analisi AI di una email in arrivo.
    /// </summary>
    /// <param name="IsSupportRequest">True se l'email è una richiesta di assistenza che merita un ticket.</param>
    /// <param name="Confidence">Confidenza del verdetto (0..1).</param>
    /// <param name="Title">Titolo sintetico della richiesta.</param>
    /// <param name="Summary">Riassunto operativo, destinato alla descrizione del ticket.</param>
    /// <param name="Reason">Motivazione del verdetto, mostrata all'operatore.</param>
    /// <param name="Language">Codice lingua ISO rilevato dell'email (es. "it", "en"), per rispondere nella stessa lingua.</param>
    public sealed record InboundEmailTriage(
        bool IsSupportRequest,
        double Confidence,
        string? Title,
        string? Summary,
        string? Reason,
        string? Language);

    /// <summary>
    /// Analizza le email in arrivo con l'AI: produce un riassunto operativo e valuta se la richiesta
    /// merita l'apertura di un ticket. È sempre <b>non bloccante</b>: se l'AI non è configurata,
    /// fallisce o va in errore, ritorna null e la pipeline prosegue col comportamento predefinito.
    /// </summary>
    public interface IInboundEmailAiService
    {
        /// <summary>True se l'AI è configurata e utilizzabile.</summary>
        bool IsAvailable { get; }

        /// <summary>Analizza l'email; null se l'AI non è disponibile o l'analisi non è riuscita.</summary>
        Task<InboundEmailTriage?> AnalyzeAsync(string subject, string? body, CancellationToken ct = default);
    }
}
