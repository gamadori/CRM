namespace CRM.Server.Services.TicketRouting
{
    /// <summary>Gruppo proposto al modello, con le informazioni su cui puo' basare la scelta.</summary>
    /// <param name="Id">Identificativo del gruppo: e' l'unico valore che il modello puo' restituire.</param>
    /// <param name="Name">Nome del gruppo.</param>
    /// <param name="Description">Descrizione generica del gruppo.</param>
    /// <param name="Hints">Competenze curate per lo smistamento (Group.AiRoutingHints).</param>
    public record TicketRoutingCandidate(int Id, string Name, string? Description, string? Hints);

    /// <summary>Tutto cio' che il modello vede del ticket da smistare.</summary>
    public record TicketRoutingRequest(
        string TicketType,
        string Description,
        string? Company,
        string? Product,
        IReadOnlyList<TicketRoutingCandidate> Candidates);

    /// <summary>Esito grezzo della chiamata al modello, prima di soglia e controlli.</summary>
    /// <param name="GroupId">Gruppo scelto, o null se il modello non se la sente di decidere.</param>
    /// <param name="Confidence">Confidenza dichiarata, tra 0 e 1.</param>
    /// <param name="Reason">Motivazione in una frase, mostrata all'operatore.</param>
    public record TicketRoutingSuggestion(int? GroupId, double Confidence, string? Reason);

    /// <summary>
    /// Chiamata al modello che sceglie il gruppo. Isolata dietro un'interfaccia per due motivi:
    /// la logica di smistamento resta testabile senza rete, e un domani si puo' cambiare fornitore
    /// senza toccare le regole di soglia e fallback.
    /// </summary>
    public interface ITicketRoutingAiClient
    {
        /// <summary>False quando manca la chiave API: lo smistamento non viene nemmeno tentato.</summary>
        bool IsAvailable { get; }

        /// <summary>Modello usato di default (senza override dalle impostazioni).</summary>
        string Model { get; }

        Task<TicketRoutingSuggestion?> SuggestGroupAsync(TicketRoutingRequest request, string? modelOverride = null, CancellationToken ct = default);
    }
}
