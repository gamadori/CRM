using System;
using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Risultato dell'estrazione automatica da scontrino/fattura tramite Azure Form Recognizer
    /// </summary>
    public class ReceiptExtractionResult
    {
        /// <summary>
        /// Successo dell'estrazione
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Messaggio di errore (se Success = false)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Tipo di documento rilevato (Receipt, Invoice, Unknown)
        /// </summary>
        public string DocumentType { get; set; }

        public int? PageFrom { get; set; }
        public int? PageTo { get; set; }

        /// <summary>
        /// Totale estratto (campo principale)
        /// </summary>
        public decimal? TotalAmount { get; set; }

        /// <summary>
        /// Confidence del totale (0-1)
        /// </summary>
        public float? TotalConfidence { get; set; }

        /// <summary>
        /// Subtotale (pre-tasse)
        /// </summary>
        public decimal? SubtotalAmount { get; set; }

        /// <summary>
        /// IVA/Tax totale
        /// </summary>
        public decimal? TaxAmount { get; set; }

        /// <summary>
        /// Data transazione
        /// </summary>
        public DateTime? TransactionDate { get; set; }

        /// <summary>
        /// Ora transazione
        /// </summary>
        public TimeSpan? TransactionTime { get; set; }

        /// <summary>
        /// Nome commerciante/fornitore
        /// </summary>
        public string MerchantName { get; set; }

        /// <summary>
        /// Indirizzo commerciante
        /// </summary>
        public string MerchantAddress { get; set; }

        /// <summary>
        /// Numero telefono commerciante
        /// </summary>
        public string MerchantPhoneNumber { get; set; }

        /// <summary>
        /// Descrizione estratta (combinazione campi rilevanti)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Valuta (EUR, USD, ecc.)
        /// </summary>
        /// <summary>Codice ISO della valuta. Null quando non e' determinabile con certezza.</summary>
        public string Currency { get; set; }

        /// <summary>
        /// Simbolo letto sullo scontrino ($, €, ¥...). Serve quando il codice ISO manca: il
        /// simbolo c'e' quasi sempre, ed e' informazione utile anche se non basta da sola.
        /// </summary>
        public string CurrencySymbol { get; set; }

        /// <summary>
        /// Valute compatibili con il simbolo letto, la piu' probabile per prima.
        /// <para>
        /// Popolata solo quando il simbolo e' ambiguo: "$" puo' essere USD, CAD, AUD...; "¥" JPY
        /// o CNY. Serve a proporre una scelta invece di lasciare il campo vuoto, senza pero'
        /// decidere al posto di chi registra: una valuta sbagliata falsa la conversione.
        /// </para>
        /// </summary>
        public List<string> CurrencyCandidates { get; set; } = new();

        /// <summary>
        /// Da dove viene la valuta quando non e' scritta accanto all'importo: il codice letto nel
        /// testo, il simbolo ambiguo, l'indirizzo dell'esercente. Si mostra a chi registra, perche'
        /// una proposta che non dice su cosa si basa non e' verificabile.
        /// </summary>
        public string CurrencyHint { get; set; }

        /// <summary>
        /// Tipologia di spesa PROPOSTA per questo documento. Null quando nessun livello ha
        /// saputo dirlo con sufficiente sicurezza: il campo resta da compilare, che e' una
        /// risposta migliore di una voce di rimborso sbagliata.
        /// </summary>
        public ExpenseCategory? SuggestedCategory { get; set; }

        /// <summary>Chi l'ha proposta: tipo di documento, regola sull'esercente o modello.</summary>
        public ExpenseCategorySource? CategorySource { get; set; }

        /// <summary>Confidenza della proposta (0-1).</summary>
        public double? CategoryConfidence { get; set; }

        /// <summary>Perche' quella tipologia: si mostra accanto al campo, per poterla verificare.</summary>
        public string CategoryReason { get; set; }

        /// <summary>
        /// Elementi riga (prodotti/servizi)
        /// </summary>
        public List<ReceiptLineItem> LineItems { get; set; } = new();

        /// <summary>
        /// Documenti fiscali distinti letti dallo stesso file, per PDF multi-pagina/multi-ricevuta.
        /// Il risultato principale contiene i totali aggregati; questa lista conserva il dettaglio
        /// da mostrare nei report senza duplicare l'allegato.
        /// </summary>
        public List<ReceiptExtractionResult> DocumentResults { get; set; } = new();

        /// <summary>
        /// Campi raw estratti (per debug)
        /// </summary>
        public Dictionary<string, object> RawFields { get; set; } = new();

        /// <summary>
        /// Confidence media globale
        /// </summary>
        public float? AverageConfidence { get; set; }

        /// <summary>
        /// Tempo di elaborazione (ms)
        /// </summary>
        public long ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Singola riga estratta da scontrino/fattura
    /// </summary>
    public class ReceiptLineItem
    {
        /// <summary>
        /// Descrizione prodotto/servizio
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Quantità
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// Prezzo unitario
        /// </summary>
        public decimal? UnitPrice { get; set; }

        /// <summary>
        /// Totale riga
        /// </summary>
        public decimal? TotalPrice { get; set; }

        /// <summary>
        /// Confidence estrazione (0-1)
        /// </summary>
        public float? Confidence { get; set; }
    }

    /// <summary>
    /// Che cosa dichiara di caricare chi carica.
    /// <para>
    /// Serve solo per i PDF, dove un file puo' essere una fattura (un documento solo, magari su
    /// piu' pagine) o una scansione di scontrini (piu' documenti), e i due casi vogliono modelli
    /// diversi. Senza dichiarazione si eseguono entrambi e vince il migliore: e' corretto ma costa
    /// due analisi. Dichiararlo ne fa risparmiare una.
    /// </para>
    /// <para>
    /// Resta un <b>indizio</b>, non un ordine: se il modello dichiarato non cava niente si prova
    /// comunque l'altro, perche' nessuno deve restare senza estrazione per una tendina sbagliata.
    /// </para>
    /// </summary>
    public enum ReceiptDocumentKind
    {
        /// <summary>Non dichiarato: si provano entrambi i modelli.</summary>
        Unknown = 0,

        /// <summary>Fattura o ricevuta fiscale emessa.</summary>
        Invoice = 1,

        /// <summary>Uno o piu' scontrini, tipicamente scansionati insieme.</summary>
        Receipts = 2
    }

    /// <summary>
    /// Richiesta di elaborazione receipt
    /// </summary>
    public class ProcessReceiptRequest
    {
        /// <summary>Tipo dichiarato da chi carica; vedi <see cref="ReceiptDocumentKind"/>.</summary>
        public ReceiptDocumentKind Kind { get; set; } = ReceiptDocumentKind.Unknown;

        /// <summary>
        /// ID dell'attachment file caricato
        /// </summary>
        public int AttachmentFileId { get; set; }

        /// <summary>
        /// ID ticket (opzionale, per contesto)
        /// </summary>
        public int? IdTicket { get; set; }

        /// <summary>
        /// Usa modello custom invece di prebuilt (opzionale)
        /// </summary>
        public bool UseCustomModel { get; set; } = false;

        /// <summary>
        /// ID modello custom (se UseCustomModel = true)
        /// </summary>
        public string CustomModelId { get; set; }
    }
}
