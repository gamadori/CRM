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
        public string Currency { get; set; }

        /// <summary>
        /// Elementi riga (prodotti/servizi)
        /// </summary>
        public List<ReceiptLineItem> LineItems { get; set; } = new();

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
    /// Richiesta di elaborazione receipt
    /// </summary>
    public class ProcessReceiptRequest
    {
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
