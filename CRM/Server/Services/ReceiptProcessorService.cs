using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CRM.Server.Data;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class ReceiptProcessorService : IReceiptProcessorService
    {
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly ILogger<ReceiptProcessorService> _logger;
        private readonly DocumentAnalysisClient _formRecognizerClient;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public ReceiptProcessorService(
            ApplicationDbContext context,
            IArchiveService archiveService,
            ILogger<ReceiptProcessorService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Attachments; // Imposta il tipo di archivio per le ricevute
            _logger = logger;

            _endpoint = configuration["AzureFormRecognizer:Endpoint"];
            _apiKey = configuration["AzureFormRecognizer:ApiKey"];

            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Azure Form Recognizer non configurato. Verifica appsettings.json");
            }
            else
            {
                var credential = new AzureKeyCredential(_apiKey);
                _formRecognizerClient = new DocumentAnalysisClient(new Uri(_endpoint), credential);
                _logger.LogInformation("Azure Form Recognizer client inizializzato: {Endpoint}", _endpoint);
            }
        }

        public async Task<ReceiptExtractionResult> ProcessReceiptAsync(
            int attachmentFileId,
            bool useCustomModel = false,
            string customModelId = null)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_formRecognizerClient == null)
                    return NotConfigured(stopwatch);

                var file = await _context.AttachmentFiles
                    .Where(x => x.Id == attachmentFileId)
                    .FirstOrDefaultAsync();

                if (file == null)
                    return Error($"File con ID {attachmentFileId} non trovato", stopwatch);

                var fileBytes = _archiveService.GetAttachment(file.Id, file.Name);
                if (fileBytes == null || fileBytes.Length == 0)
                    return Error("File vuoto o non trovato nell'archivio", stopwatch);

                return await ProcessReceiptFromBytesAsync(fileBytes, file.Name, useCustomModel, customModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione del file {FileId}", attachmentFileId);
                return Error($"Errore durante l'elaborazione: {ex.Message}", stopwatch);
            }
        }

        public async Task<ReceiptExtractionResult> ProcessReceiptFromBytesAsync(
            byte[] fileBytes,
            string fileName,
            bool useCustomModel = false,
            string customModelId = null)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_formRecognizerClient == null)
                    return NotConfigured(stopwatch);

                // Try prebuilt-receipt first, then fall back to prebuilt-document
                string modelId = useCustomModel && !string.IsNullOrEmpty(customModelId)
                    ? customModelId
                    : "prebuilt-receipt";

                _logger.LogInformation("Elaborazione file '{FileName}' ({Bytes} bytes) con modello '{ModelId}'",
                    fileName, fileBytes.Length, modelId);

                using var stream = new MemoryStream(fileBytes);
                var operation = await _formRecognizerClient.AnalyzeDocumentAsync(
                    WaitUntil.Completed, modelId, stream);

                var result = operation.Value;

                _logger.LogInformation("Analisi completata. Documenti trovati: {Count}", result.Documents.Count);

                if (result.Documents.Count == 0)
                {
                    // Log raw content to help debug
                    _logger.LogWarning("Nessun documento strutturato rilevato. Pagine: {Pages}, Parole: {Words}",
                        result.Pages.Count,
                        result.Pages.Sum(p => p.Words.Count));

                    return Error(
                        "Nessun documento rilevato. Assicurati che lo scontrino/fattura sia leggibile e non ruotato.",
                        stopwatch);
                }

                var document = result.Documents.First();

                _logger.LogInformation("Documento tipo '{DocType}', Confidence={Conf:P1}, Campi disponibili: [{Fields}]",
                    document.DocumentType,
                    document.Confidence,
                    string.Join(", ", document.Fields.Keys));

                // Log every field raw value for diagnosis
                foreach (var kv in document.Fields)
                {
                    _logger.LogDebug("  Campo '{Key}': tipo={Type}, confidence={Conf}, content='{Content}'",
                        kv.Key, kv.Value.FieldType, kv.Value.Confidence, kv.Value.Content);
                }

                var extractionResult = new ReceiptExtractionResult
                {
                    Success = true,
                    DocumentType = document.DocumentType,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };

                ExtractCommonFields(document, extractionResult);
                ExtractLineItems(document, extractionResult);
                CalculateAverageConfidence(extractionResult);

                _logger.LogInformation(
                    "Estrazione completata: Totale={Total}, IVA={Tax}, Data={Date}, Commerciante='{Merchant}', Confidence={Conf:P1}, Tempo={Time}ms",
                    extractionResult.TotalAmount,
                    extractionResult.TaxAmount,
                    extractionResult.TransactionDate,
                    extractionResult.MerchantName,
                    extractionResult.AverageConfidence,
                    stopwatch.ElapsedMilliseconds);

                return extractionResult;
            }
            catch (RequestFailedException ex) when (ex.Status == 400)
            {
                _logger.LogWarning(ex, "File non supportato o danneggiato: '{FileName}'", fileName);
                return Error("File non supportato. Usa immagini (JPG, PNG) o PDF leggibili.", stopwatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione Azure Form Recognizer per '{FileName}'", fileName);
                return Error($"Errore durante l'elaborazione: {ex.Message}", stopwatch);
            }
        }

        /// <summary>
        /// Estrae i campi principali coprendo sia prebuilt-receipt che prebuilt-invoice.
        /// NON usa controlli strict sul FieldType: legge il content grezzo come fallback.
        /// </summary>
        private void ExtractCommonFields(AnalyzedDocument document, ReceiptExtractionResult result)
        {
            var f = document.Fields;

            // ?? TOTALE ????????????????????????????????????????????????????????????????
            // prebuilt-receipt ? "Total"
            // prebuilt-invoice ? "InvoiceTotal", "AmountDue", "TotalAmount"
            result.TotalAmount = TryGetCurrency(f, out var totalConf,
                "Total", "InvoiceTotal", "AmountDue", "TotalAmount", "SubTotal");
            result.TotalConfidence = totalConf;

            if (result.TotalAmount.HasValue)
                _logger.LogInformation("Totale estratto: {Amount} (confidence {Conf})", result.TotalAmount, totalConf);
            else
                _logger.LogWarning("Totale NON estratto. Campi cercati: Total, InvoiceTotal, AmountDue, TotalAmount");

            // ?? SUBTOTALE ?????????????????????????????????????????????????????????????
            result.SubtotalAmount = TryGetCurrency(f, out _,
                "Subtotal", "SubTotal", "NetAmount", "TaxableAmount");

            // ?? IVA ???????????????????????????????????????????????????????????????????
            // prebuilt-receipt ? "TotalTax"
            // prebuilt-invoice ? "TotalTax", "TaxAmount", "TotalVAT"
            result.TaxAmount = TryGetCurrency(f, out _,
                "TotalTax", "TaxAmount", "TotalVAT", "Tax", "VATAmount");

            // ?? VALUTA ????????????????????????????????????????????????????????????????
            result.Currency = TryGetCurrencyCode(f,
                "Total", "InvoiceTotal", "AmountDue", "TotalAmount") ?? "EUR";

            // ?? DATA TRANSAZIONE ??????????????????????????????????????????????????????
            // prebuilt-receipt ? "TransactionDate"
            // prebuilt-invoice ? "InvoiceDate", "DueDate"
            result.TransactionDate = TryGetDate(f,
                "TransactionDate", "InvoiceDate", "DueDate", "ServiceDate");

            // ?? ORA ???????????????????????????????????????????????????????????????????
            result.TransactionTime = TryGetTime(f, "TransactionTime");

            // ?? COMMERCIANTE / FORNITORE ??????????????????????????????????????????????
            // prebuilt-receipt ? "MerchantName"
            // prebuilt-invoice ? "VendorName", "SupplierName"
            result.MerchantName = TryGetString(f,
                "MerchantName", "VendorName", "SupplierName", "CustomerName");

            // ?? INDIRIZZO ?????????????????????????????????????????????????????????????
            result.MerchantAddress = TryGetString(f,
                "MerchantAddress", "VendorAddress", "SupplierAddress", "BillingAddress", "ShippingAddress");

            // ?? TELEFONO ?????????????????????????????????????????????????????????????
            // Azure returns phone as PhoneNumber type, not String — read .Content directly
            result.MerchantPhoneNumber = TryGetContent(f,
                "MerchantPhoneNumber", "VendorPhone", "SupplierPhone");

            // ?? DESCRIZIONE AUTO-GENERATA ?????????????????????????????????????????????
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.MerchantName)) parts.Add(result.MerchantName);
            if (result.TransactionDate.HasValue) parts.Add(result.TransactionDate.Value.ToString("dd/MM/yyyy"));
            if (result.TotalAmount.HasValue) parts.Add(result.TotalAmount.Value.ToString("C2", CultureInfo.GetCultureInfo("it-IT")));
            result.Description = string.Join(" - ", parts);
        }

        private void ExtractLineItems(AnalyzedDocument document, ReceiptExtractionResult result)
        {
            // prebuilt-receipt ? "Items"
            // prebuilt-invoice ? "Items" (same key)
            if (!document.Fields.TryGetValue("Items", out var itemsField))
                return;

            if (itemsField.FieldType != DocumentFieldType.List)
            {
                _logger.LogDebug("Campo 'Items' trovato ma tipo = {Type}, atteso List", itemsField.FieldType);
                return;
            }

            IReadOnlyList<DocumentField> items;
            try { items = itemsField.Value.AsList(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Impossibile leggere lista Items"); return; }

            foreach (var item in items)
            {
                if (item.FieldType != DocumentFieldType.Dictionary) continue;

                IReadOnlyDictionary<string, DocumentField> dict;
                try { dict = item.Value.AsDictionary(); }
                catch { continue; }

                var lineItem = new ReceiptLineItem();

                // Description
                if (dict.TryGetValue("Description", out var descF))
                {
                    lineItem.Description = SafeGetString(descF);
                    lineItem.Confidence = descF.Confidence;
                }

                // Quantity — can be Double, Int64 or String
                if (dict.TryGetValue("Quantity", out var qtyF))
                    lineItem.Quantity = SafeGetDecimal(qtyF);

                // Unit price
                if (dict.TryGetValue("UnitPrice", out var upF))
                    lineItem.UnitPrice = SafeGetCurrencyAmount(upF);

                // Total price — "TotalPrice" in receipt, "Amount" in invoice
                if (dict.TryGetValue("TotalPrice", out var tpF))
                    lineItem.TotalPrice = SafeGetCurrencyAmount(tpF);
                else if (dict.TryGetValue("Amount", out var amtF))
                    lineItem.TotalPrice = SafeGetCurrencyAmount(amtF);

                result.LineItems.Add(lineItem);
            }

            _logger.LogInformation("Estratti {Count} line items", result.LineItems.Count);
        }

        private void CalculateAverageConfidence(ReceiptExtractionResult result)
        {
            var list = new List<float>();
            if (result.TotalConfidence.HasValue) list.Add(result.TotalConfidence.Value);
            foreach (var item in result.LineItems)
                if (item.Confidence.HasValue) list.Add(item.Confidence.Value);
            if (list.Any())
                result.AverageConfidence = list.Average();
        }

        public async Task<bool> HealthCheckAsync()
        {
            try { return await Task.FromResult(_formRecognizerClient != null); }
            catch (Exception ex) { _logger.LogError(ex, "Health check fallito"); return false; }
        }

        // ?? HELPERS ??????????????????????????????????????????????????????????????????

        /// <summary>
        /// Tenta di leggere il valore monetario da una lista ordinata di nomi campo.
        /// Accetta sia Currency che Double/String come fallback.
        /// </summary>
        private decimal? TryGetCurrency(
            IReadOnlyDictionary<string, DocumentField> fields,
            out float? confidence,
            params string[] names)
        {
            confidence = null;
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                var amount = SafeGetCurrencyAmount(field);
                if (amount.HasValue)
                {
                    confidence = field.Confidence;
                    _logger.LogDebug("Campo '{Name}' letto come importo: {Amount}", name, amount);
                    return amount;
                }
            }
            return null;
        }

        private string TryGetCurrencyCode(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                if (field.FieldType == DocumentFieldType.Currency)
                {
                    try { return field.Value.AsCurrency().Code; } catch { }
                }
            }
            return null;
        }

        private DateTime? TryGetDate(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                try
                {
                    if (field.FieldType == DocumentFieldType.Date)
                        return field.Value.AsDate().DateTime;

                    // Fallback: parse from content string
                    if (!string.IsNullOrWhiteSpace(field.Content) &&
                        DateTime.TryParse(field.Content, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        return parsed;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Impossibile leggere data dal campo '{Name}'", name);
                }
            }
            return null;
        }

        private TimeSpan? TryGetTime(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                try
                {
                    if (field.FieldType == DocumentFieldType.Time)
                        return field.Value.AsTime();
                }
                catch { }
            }
            return null;
        }

        private string TryGetString(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                var s = SafeGetString(field);
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return null;
        }

        /// <summary>
        /// Reads the raw Content property — works for PhoneNumber, Address, etc.
        /// </summary>
        private string TryGetContent(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                if (!string.IsNullOrWhiteSpace(field.Content)) return field.Content;
            }
            return null;
        }

        /// <summary>
        /// Reads a string value regardless of the actual FieldType reported by Azure.
        /// Falls back to .Content if .AsString() is not applicable.
        /// </summary>
        private static string SafeGetString(DocumentField field)
        {
            try
            {
                if (field.FieldType == DocumentFieldType.String)
                    return field.Value.AsString();
            }
            catch { }

            // Fallback: raw content recognized by OCR
            return field.Content;
        }

        /// <summary>
        /// Reads a decimal amount from Currency, Double, Int64 or String field types.
        /// This is the key fix: Azure sometimes returns the same field as Currency or Double
        /// depending on the document and model confidence.
        /// </summary>
        private decimal? SafeGetCurrencyAmount(DocumentField field)
        {
            try
            {
                if (field.FieldType == DocumentFieldType.Currency)
                    return (decimal?)field.Value.AsCurrency().Amount;
            }
            catch { }

            try
            {
                if (field.FieldType == DocumentFieldType.Double)
                    return (decimal?)field.Value.AsDouble();
            }
            catch { }

            try
            {
                if (field.FieldType == DocumentFieldType.Int64)
                    return (decimal)field.Value.AsInt64();
            }
            catch { }

            // Last resort: parse the raw OCR content
            if (!string.IsNullOrWhiteSpace(field.Content))
            {
                // Remove currency symbols and whitespace, normalise decimal separator
                var raw = field.Content
                    .Replace("€", "").Replace("$", "").Replace("£", "")
                    .Replace(" ", "").Trim();

                // Handle Italian format: 1.234,56 ? 1234.56
                if (raw.Contains(',') && raw.Contains('.'))
                    raw = raw.Replace(".", "").Replace(",", ".");
                else if (raw.Contains(',') && !raw.Contains('.'))
                    raw = raw.Replace(",", ".");

                if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }

            return null;
        }

        private static decimal? SafeGetDecimal(DocumentField field)
        {
            try
            {
                if (field.FieldType == DocumentFieldType.Double)
                    return (decimal?)field.Value.AsDouble();
            }
            catch { }

            try
            {
                if (field.FieldType == DocumentFieldType.Int64)
                    return (decimal)field.Value.AsInt64();
            }
            catch { }

            try
            {
                if (field.FieldType == DocumentFieldType.String &&
                    decimal.TryParse(field.Value.AsString(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var s))
                    return s;
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(field.Content) &&
                decimal.TryParse(field.Content, NumberStyles.Any, CultureInfo.InvariantCulture, out var c))
                return c;

            return null;
        }

        private static ReceiptExtractionResult NotConfigured(Stopwatch sw) =>
            new() { Success = false, ErrorMessage = "Azure Form Recognizer non configurato. Verifica appsettings.json", ProcessingTimeMs = sw.ElapsedMilliseconds };

        private static ReceiptExtractionResult Error(string msg, Stopwatch sw) =>
            new() { Success = false, ErrorMessage = msg, ProcessingTimeMs = sw.ElapsedMilliseconds };
    }
}
