using CRM.Shared;

namespace CRM.Server.Services.ExpenseCategorization
{
    /// <summary>
    /// Quello che si sa di una spesa nel momento in cui bisogna dirne la tipologia: quel che ha
    /// letto l'OCR, niente di piu'. Volutamente senza id ne' entita': cosi' le regole si provano
    /// senza database e senza rete.
    /// </summary>
    /// <param name="MerchantName">Esercente o fornitore letto sul documento.</param>
    /// <param name="DocumentType">Sottotipo restituito dall'OCR (receipt.hotel, invoice...).</param>
    /// <param name="Lines">Descrizioni delle righe, quando ci sono.</param>
    /// <param name="Description">Descrizione della spesa, se gia' compilata.</param>
    /// <param name="TotalAmount">Importo: da solo non classifica, ma aiuta il modello a scegliere.</param>
    /// <param name="Currency">Valuta dell'importo.</param>
    public record ExpenseCategoryRequest(
        string? MerchantName,
        string? DocumentType,
        IReadOnlyList<string> Lines,
        string? Description = null,
        decimal? TotalAmount = null,
        string? Currency = null)
    {
        public static ExpenseCategoryRequest Empty { get; } =
            new(null, null, Array.Empty<string>());
    }

    /// <summary>
    /// Tipologia proposta, con la sua confidenza e il motivo. <c>Category</c> null significa
    /// "non lo so": e' una risposta valida, e vale piu' di un "Altro" messo per riempire.
    /// </summary>
    public record ExpenseCategorySuggestion(
        ExpenseCategory? Category,
        double Confidence,
        string? Reason,
        ExpenseCategorySource Source)
    {
        public static ExpenseCategorySuggestion None { get; } =
            new(null, 0, null, ExpenseCategorySource.Manual);

        public bool HasCategory => Category.HasValue;
    }

    /// <summary>
    /// I tre livelli in cascata: sottotipo del documento, regole su esercente e righe, e solo
    /// se tacciono entrambi il modello. Chi chiama non sa quale ha risposto - lo dice
    /// <see cref="ExpenseCategorySuggestion.Source"/>.
    /// </summary>
    public interface IExpenseCategorizer
    {
        /// <summary>
        /// Classifica piu' documenti insieme. La lista in uscita ha sempre la stessa lunghezza e
        /// lo stesso ordine di quella in entrata: l'i-esima proposta e' dell'i-esimo documento.
        /// </summary>
        Task<IReadOnlyList<ExpenseCategorySuggestion>> CategorizeAsync(
            IReadOnlyList<ExpenseCategoryRequest> requests,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Il modello, dietro un'interfaccia sua: le regole devono restare verificabili senza rete,
    /// e questa e' l'unica parte che una chiave API mancante puo' spegnere.
    /// </summary>
    public interface IExpenseCategoryAiClient
    {
        bool IsAvailable { get; }

        string Model { get; }

        /// <summary>
        /// Una sola chiamata per tutti i documenti rimasti senza tipologia. Restituisce una
        /// proposta per ciascuno, nello stesso ordine; null se la chiamata non e' utilizzabile.
        /// </summary>
        Task<IReadOnlyList<ExpenseCategorySuggestion>?> SuggestAsync(
            IReadOnlyList<ExpenseCategoryRequest> requests,
            CancellationToken ct = default);
    }
}
