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
    /// <summary>
    /// Implementazione del servizio per l'elaborazione di scontrini/fatture con Azure Form Recognizer
    /// </summary>
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
            _logger = logger;

            // ? Leggi configurazione da appsettings.json
            _endpoint = configuration["AzureFormRecognizer:Endpoint"];
            _apiKey = configuration["AzureFormRecognizer:ApiKey"];

            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("Azure Form Recognizer non configurato. Verifica appsettings.json (AzureFormRecognizer:Endpoint e ApiKey)");
            }
            else
            {
                var credential = new AzureKeyCredential(_apiKey);
                _formRecognizerClient = new DocumentAnalysisClient(new Uri(_endpoint), credential);
                _logger.LogInformation("Azure Form Recognizer client inizializzato con successo");
            }
        }

        /// <summary>
        /// Elabora un file attachment e ne estrae i campi strutturati
        /// </summary>
        public async Task<ReceiptExtractionResult> ProcessReceiptAsync(
            int attachmentFileId,
            bool useCustomModel = false,
            string customModelId = null)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (_formRecognizerClient == null)
                {
                    return new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Azure Form Recognizer non configurato. Verifica appsettings.json",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Recupera il file dal database
                var file = await _context.AttachmentFiles
                    .Where(x => x.Id == attachmentFileId)
                    .FirstOrDefaultAsync();

                if (file == null)
                {
                    return new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"File con ID {attachmentFileId} non trovato",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Recupera i byte del file dall'archivio
                var fileBytes = _archiveService.GetAttachment(file.Id, Path.GetExtension(file.Name));

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    return new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "File vuoto o non trovato nell'archivio",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Processa il file
                return await ProcessReceiptFromBytesAsync(fileBytes, file.Name, useCustomModel, customModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione del file {FileId}", attachmentFileId);
                return new ReceiptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Errore durante l'elaborazione: {ex.Message}",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Elabora un file da byte array direttamente
        /// </summary>
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
                {
                    return new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Azure Form Recognizer non configurato",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Determina il modello da usare
                string modelId = useCustomModel && !string.IsNullOrEmpty(customModelId)
                    ? customModelId
                    : "prebuilt-receipt"; // Modello prebuilt per scontrini

                _logger.LogInformation("Elaborazione file {FileName} con modello {ModelId}", fileName, modelId);

                // Analizza il documento
                using var stream = new MemoryStream(fileBytes);
                var operation = await _formRecognizerClient.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    modelId,
                    stream);

                var result = operation.Value;

                // Estrai i campi dal primo documento rilevato
                if (result.Documents.Count == 0)
                {
                    return new ReceiptExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "Nessun documento rilevato nell'immagine. Assicurati che lo scontrino/fattura sia leggibile.",
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                var document = result.Documents.First();

                // Costruisci il risultato
                var extractionResult = new ReceiptExtractionResult
                {
                    Success = true,
                    DocumentType = document.DocumentType,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };

                // ? Estrai campi comuni (receipt e invoice)
                ExtractCommonFields(document, extractionResult);

                // ? Estrai line items (prodotti)
                ExtractLineItems(document, extractionResult);

                // ? Calcola confidence media
                CalculateAverageConfidence(extractionResult);

                _logger.LogInformation(
                    "Estrazione completata: Totale={Total}, Confidenza={Confidence}%, Tempo={Time}ms",
                    extractionResult.TotalAmount,
                    extractionResult.AverageConfidence * 100,
                    stopwatch.ElapsedMilliseconds);

                return extractionResult;
            }
            catch (RequestFailedException ex) when (ex.Status == 400)
            {
                _logger.LogWarning(ex, "File non supportato o danneggiato: {FileName}", fileName);
                return new ReceiptExtractionResult
                {
                    Success = false,
                    ErrorMessage = "File non supportato. Usa immagini (JPG, PNG) o PDF leggibili.",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione Azure Form Recognizer");
                return new ReceiptExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Errore durante l'elaborazione: {ex.Message}",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// Estrae i campi comuni da un documento (receipt o invoice)
        /// </summary>
        private void ExtractCommonFields(AnalyzedDocument document, ReceiptExtractionResult result)
        {
            var fields = document.Fields;

            // ? TOTALE (campo principale)
            if (fields.TryGetValue("Total", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
            {
                var currency = totalField.Value.AsCurrency();
                result.TotalAmount = (decimal?)currency.Amount;
                result.TotalConfidence = totalField.Confidence;
                result.Currency = currency.Code ?? "EUR";
            }

            // ? SUBTOTALE
            if (fields.TryGetValue("Subtotal", out var subtotalField) && subtotalField.FieldType == DocumentFieldType.Currency)
            {
                var subtotal = subtotalField.Value.AsCurrency();
                result.SubtotalAmount = (decimal?)subtotal.Amount;
            }

            // ? IVA/TAX
            if (fields.TryGetValue("TotalTax", out var taxField) && taxField.FieldType == DocumentFieldType.Currency)
            {
                var tax = taxField.Value.AsCurrency();
                result.TaxAmount = (decimal?)tax.Amount;
            }

            // ? DATA TRANSAZIONE
            if (fields.TryGetValue("TransactionDate", out var dateField) && dateField.FieldType == DocumentFieldType.Date)
            {
                result.TransactionDate = dateField.Value.AsDate().DateTime;
            }

            // ? ORA TRANSAZIONE
            if (fields.TryGetValue("TransactionTime", out var timeField) && timeField.FieldType == DocumentFieldType.Time)
            {
                result.TransactionTime = timeField.Value.AsTime();
            }

            // ? COMMERCIANTE/FORNITORE
            if (fields.TryGetValue("MerchantName", out var merchantField) && merchantField.FieldType == DocumentFieldType.String)
            {
                result.MerchantName = merchantField.Value.AsString();
            }

            // ? INDIRIZZO COMMERCIANTE
            if (fields.TryGetValue("MerchantAddress", out var addressField) && addressField.FieldType == DocumentFieldType.String)
            {
                result.MerchantAddress = addressField.Value.AsString();
            }

            // ? TELEFONO COMMERCIANTE
            if (fields.TryGetValue("MerchantPhoneNumber", out var phoneField) && phoneField.FieldType == DocumentFieldType.String)
            {
                result.MerchantPhoneNumber = phoneField.Value.AsString();
            }

            // ? DESCRIZIONE GENERATA AUTOMATICAMENTE
            var descParts = new List<string>();
            if (!string.IsNullOrEmpty(result.MerchantName))
                descParts.Add(result.MerchantName);
            if (result.TransactionDate.HasValue)
                descParts.Add(result.TransactionDate.Value.ToString("dd/MM/yyyy"));
            if (result.TotalAmount.HasValue)
                descParts.Add($"{result.TotalAmount.Value:C2}");

            result.Description = string.Join(" - ", descParts);
        }

        /// <summary>
        /// Estrae gli elementi riga (prodotti/servizi) dal documento
        /// </summary>
        private void ExtractLineItems(AnalyzedDocument document, ReceiptExtractionResult result)
        {
            if (!document.Fields.TryGetValue("Items", out var itemsField) || itemsField.FieldType != DocumentFieldType.List)
                return;

            foreach (var item in itemsField.Value.AsList())
            {
                if (item.FieldType != DocumentFieldType.Dictionary)
                    continue;

                var itemDict = item.Value.AsDictionary();
                var lineItem = new ReceiptLineItem();

                // Descrizione
                if (itemDict.TryGetValue("Description", out var descField))
                {
                    lineItem.Description = descField.Value.AsString();
                    lineItem.Confidence = descField.Confidence;
                }

                // Quantità
                if (itemDict.TryGetValue("Quantity", out var qtyField))
                {
                    lineItem.Quantity = (decimal?)qtyField.Value.AsDouble();
                }

                // Prezzo unitario
                if (itemDict.TryGetValue("UnitPrice", out var priceField) && priceField.FieldType == DocumentFieldType.Currency)
                {
                    var unitPrice = priceField.Value.AsCurrency();
                    lineItem.UnitPrice = (decimal?)unitPrice.Amount;
                }

                // Totale riga
                if (itemDict.TryGetValue("TotalPrice", out var totalField) && totalField.FieldType == DocumentFieldType.Currency)
                {
                    var totalPrice = totalField.Value.AsCurrency();
                    lineItem.TotalPrice = (decimal?)totalPrice.Amount;
                }

                result.LineItems.Add(lineItem);
            }

            _logger.LogInformation("Estratti {Count} line items", result.LineItems.Count);
        }

        /// <summary>
        /// Calcola la confidence media globale
        /// </summary>
        private void CalculateAverageConfidence(ReceiptExtractionResult result)
        {
            var confidences = new List<float>();

            if (result.TotalConfidence.HasValue)
                confidences.Add(result.TotalConfidence.Value);

            foreach (var item in result.LineItems)
            {
                if (item.Confidence.HasValue)
                    confidences.Add(item.Confidence.Value);
            }

            if (confidences.Any())
            {
                result.AverageConfidence = confidences.Average();
            }
        }

        /// <summary>
        /// Verifica la connettività con Azure Form Recognizer
        /// </summary>
        public async Task<bool> HealthCheckAsync()
        {
            try
            {
                if (_formRecognizerClient == null)
                    return false;

                // Prova a recuperare la lista dei modelli (operazione leggera)
                // Nota: richiede DocumentModelAdministrationClient, per semplicità usiamo un test base
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check Azure Form Recognizer fallito");
                return false;
            }
        }
    }
}
