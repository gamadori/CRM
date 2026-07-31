using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CRM.Shared.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class AzureReceiptAnalyzer : IReceiptAnalyzer
    {
        private readonly ILogger<AzureReceiptAnalyzer> _logger;
        private readonly DocumentAnalysisClient _formRecognizerClient;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureReceiptAnalyzer(
            ILogger<AzureReceiptAnalyzer> logger,
            IConfiguration configuration)
        {
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

        public async Task<ReceiptExtractionResult> AnalyzeAsync(
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

                var documents = result.Documents;

                var coveredPages = result.Documents
                    .SelectMany(document => document.BoundingRegions)
                    .Select(region => region.PageNumber)
                    .Distinct()
                    .ToHashSet();
                var missingPages = Enumerable.Range(1, result.Pages.Count)
                    .Where(pageNumber => !coveredPages.Contains(pageNumber))
                    .ToList();

                // Se Azure ha riconosciuto un documento che copre tutte le pagine, e' una
                // fattura multipagina e resta unita. Si analizzano singolarmente solo le pagine
                // non attribuite ad alcun documento fiscale.
                if (missingPages.Count > 0)
                {
                    var pageDocuments = await AnalyzePdfPagesAsync(fileBytes, modelId, missingPages);
                    if (pageDocuments.Count > 0)
                        documents = documents.Concat(pageDocuments).ToList();
                }

                if (documents.Count == 0)
                {
                    // Log raw content to help debug
                    _logger.LogWarning("Nessun documento strutturato rilevato. Pagine: {Pages}, Parole: {Words}",
                        result.Pages.Count,
                        result.Pages.Sum(p => p.Words.Count));

                    return Error(
                        "Nessun documento rilevato. Assicurati che lo scontrino/fattura sia leggibile e non ruotato.",
                        stopwatch);
                }

                var extractionResult = ExtractDocuments(documents, stopwatch);

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

        private async Task<IReadOnlyList<AnalyzedDocument>> AnalyzePdfPagesAsync(
            byte[] fileBytes,
            string modelId,
            IReadOnlyCollection<int> pageNumbers)
        {
            var documents = new List<AnalyzedDocument>();

            _logger.LogInformation(
                "PDF multi-pagina restituito come documento unico: forzo analisi pagina per pagina ({PageCount} pagine).",
                pageNumbers.Count);

            foreach (var pageNumber in pageNumbers.OrderBy(number => number))
            {
                try
                {
                    using var pageStream = new MemoryStream(fileBytes);
                    var options = new AnalyzeDocumentOptions();
                    options.Pages.Add(pageNumber.ToString(CultureInfo.InvariantCulture));

                    var pageOperation = await _formRecognizerClient.AnalyzeDocumentAsync(
                        WaitUntil.Completed,
                        modelId,
                        pageStream,
                        options);

                    var pageResult = pageOperation.Value;

                    if (pageResult.Documents.Count == 0)
                    {
                        _logger.LogWarning("Pagina {PageNumber}: nessun documento fiscale rilevato.", pageNumber);
                        continue;
                    }

                    foreach (var document in pageResult.Documents)
                        documents.Add(document);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Errore durante l'analisi della pagina {PageNumber}.", pageNumber);
                }
            }

            _logger.LogInformation(
                "Analisi pagina per pagina completata: {Count} documenti fiscali estratti.",
                documents.Count);

            return documents;
        }

        private ReceiptExtractionResult ExtractDocuments(IReadOnlyList<AnalyzedDocument> documents, Stopwatch stopwatch)
        {
            if (documents.Count == 1)
                return ExtractDocument(documents[0], stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "PDF multi-documento rilevato: aggrego {Count} documenti restituiti da Azure.",
                documents.Count);

            var partialResults = documents
                .Select(document => ExtractDocument(document, stopwatch.ElapsedMilliseconds))
                .OrderBy(document => document.PageFrom ?? int.MaxValue)
                .ToList();

            var aggregate = new ReceiptExtractionResult
            {
                Success = true,
                DocumentType = string.Join(", ",
                    documents.Select(d => d.DocumentType)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()),
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                TotalAmount = SumAmounts(partialResults.Select(x => x.TotalAmount)),
                SubtotalAmount = SumAmounts(partialResults.Select(x => x.SubtotalAmount)),
                TaxAmount = SumAmounts(partialResults.Select(x => x.TaxAmount)),
                TransactionDate = partialResults
                    .Where(x => x.TransactionDate.HasValue)
                    .Select(x => x.TransactionDate.Value)
                    .DefaultIfEmpty()
                    .Min(),
                TransactionTime = partialResults.FirstOrDefault(x => x.TransactionTime.HasValue)?.TransactionTime,
                MerchantName = JoinDistinct(partialResults.Select(x => x.MerchantName)),
                MerchantAddress = FirstNonEmpty(partialResults.Select(x => x.MerchantAddress)),
                MerchantPhoneNumber = FirstNonEmpty(partialResults.Select(x => x.MerchantPhoneNumber)),
                CurrencySymbol = FirstNonEmpty(partialResults.Select(x => x.CurrencySymbol)),
                DocumentResults = partialResults
            };

            if (aggregate.TransactionDate == default)
                aggregate.TransactionDate = null;

            var currencies = partialResults
                .Select(x => x.Currency)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (currencies.Count == 1)
                aggregate.Currency = currencies[0];
            else
                aggregate.CurrencyCandidates = currencies
                    .Concat(partialResults.SelectMany(x => x.CurrencyCandidates ?? new List<string>()))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            aggregate.LineItems = partialResults.SelectMany(x => x.LineItems).ToList();

            for (var i = 0; i < partialResults.Count; i++)
            {
                foreach (var kv in partialResults[i].RawFields)
                    aggregate.RawFields[$"Document{i + 1}.{kv.Key}"] = kv.Value;
            }

            aggregate.TotalConfidence = Average(partialResults.Select(x => x.TotalConfidence));
            CalculateAverageConfidence(aggregate);

            var descriptionParts = new List<string> { "PDF multi-pagina" };
            if (!string.IsNullOrWhiteSpace(aggregate.MerchantName)) descriptionParts.Add(aggregate.MerchantName);
            if (aggregate.TransactionDate.HasValue) descriptionParts.Add(aggregate.TransactionDate.Value.ToString("dd/MM/yyyy"));
            if (aggregate.TotalAmount.HasValue) descriptionParts.Add(FormatAmount(aggregate));
            aggregate.Description = string.Join(" - ", descriptionParts);

            return aggregate;
        }

        private ReceiptExtractionResult ExtractDocument(AnalyzedDocument document, long processingTimeMs)
        {
            _logger.LogInformation("Documento tipo '{DocType}', Confidence={Conf:P1}, Campi disponibili: [{Fields}]",
                document.DocumentType,
                document.Confidence,
                string.Join(", ", document.Fields.Keys));

            foreach (var kv in document.Fields)
            {
                _logger.LogDebug("  Campo '{Key}': tipo={Type}, confidence={Conf}, content='{Content}'",
                    kv.Key, kv.Value.FieldType, kv.Value.Confidence, kv.Value.Content);
            }

            var extractionResult = new ReceiptExtractionResult
            {
                Success = true,
                DocumentType = document.DocumentType,
                ProcessingTimeMs = processingTimeMs
            };

            var pageNumbers = document.BoundingRegions
                .Select(region => region.PageNumber)
                .Distinct()
                .OrderBy(pageNumber => pageNumber)
                .ToList();
            if (pageNumbers.Count > 0)
            {
                extractionResult.PageFrom = pageNumbers[0];
                extractionResult.PageTo = pageNumbers[^1];
            }

            ExtractCommonFields(document, extractionResult);
            ExtractLineItems(document, extractionResult);
            CalculateAverageConfidence(extractionResult);

            return extractionResult;
        }

        private static decimal? SumAmounts(IEnumerable<decimal?> values)
        {
            var present = values.Where(x => x.HasValue).Select(x => x.Value).ToList();
            return present.Count == 0 ? null : present.Sum();
        }

        private static float? Average(IEnumerable<float?> values)
        {
            var present = values.Where(x => x.HasValue).Select(x => x.Value).ToList();
            return present.Count == 0 ? null : present.Average();
        }

        private static string FirstNonEmpty(IEnumerable<string> values) =>
            values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        private static string JoinDistinct(IEnumerable<string> values)
        {
            var present = values
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return present.Count switch
            {
                0 => null,
                1 => present[0],
                _ => string.Join(" / ", present)
            };
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
            // Niente ripiego su "EUR": una valuta non riconosciuta resta null e diventa un campo
            // da compilare. Assumere euro su uno scontrino di Tokyo produce un importo sbagliato
            // con la stessa faccia sicura di uno giusto, e nessuno se ne accorge.
            result.Currency = TryGetCurrencyCode(f,
                "Total", "InvoiceTotal", "AmountDue", "TotalAmount");

            result.CurrencySymbol = TryGetCurrencySymbol(f,
                "Total", "InvoiceTotal", "AmountDue", "TotalAmount");

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
            // Azure returns phone as PhoneNumber type, not String -> read .Content directly
            result.MerchantPhoneNumber = TryGetContent(f,
                "MerchantPhoneNumber", "VendorPhone", "SupplierPhone");

            // ?? DESCRIZIONE AUTO-GENERATA ?????????????????????????????????????????????
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.MerchantName)) parts.Add(result.MerchantName);
            if (result.TransactionDate.HasValue) parts.Add(result.TransactionDate.Value.ToString("dd/MM/yyyy"));
            if (result.TotalAmount.HasValue) parts.Add(FormatAmount(result));
            result.Description = string.Join(" - ", parts);

            ResolveCurrencyCandidates(result);
            FillCurrencyDiagnostics(f, result);
        }

        /// <summary>
        /// Importo per la descrizione automatica: separatori all'italiana, ma valuta SOLO quella
        /// letta davvero.
        /// <para>
        /// Prima si usava <c>ToString("C2", it-IT)</c>, che appiccica l'euro qualunque cosa ci
        /// fosse scritto sullo scontrino: uno scontrino di Redmond finiva descritto "14,50 €".
        /// Formattare con la cultura dell'utente e' giusto per i separatori, ma il simbolo di
        /// valuta non e' una preferenza locale — e' un dato del documento.
        /// </para>
        /// </summary>
        private static string FormatAmount(ReceiptExtractionResult result)
        {
            var amount = result.TotalAmount.Value.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));

            if (!string.IsNullOrWhiteSpace(result.Currency))
                return $"{amount} {result.Currency}";

            if (!string.IsNullOrWhiteSpace(result.CurrencySymbol))
                return $"{amount} {result.CurrencySymbol}";

            // Valuta sconosciuta: si scrive il numero e basta, senza inventare un simbolo.
            return amount;
        }

        /// <summary>
        /// Traduce in valuta il simbolo letto sul documento, quando il codice ISO non c'e'.
        /// <para>
        /// Se il simbolo identifica una sola valuta la si conclude qui: "€18.00" e' EUR, non c'e'
        /// niente da chiedere. Se ne identifica piu' d'una si propongono i candidati invece di
        /// lasciare il campo vuoto; l'indirizzo dell'esercente, quando e' inequivocabile, porta in
        /// cima quello giusto - ma la scelta resta di chi registra, perche' una valuta sbagliata
        /// falsa la conversione e quindi il rimborso.
        /// </para>
        /// </summary>
        private void ResolveCurrencyCandidates(ReceiptExtractionResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Currency))
                return;

            if (string.IsNullOrWhiteSpace(result.CurrencySymbol))
            {
                _logger.LogWarning("Valuta NON estratta e nessun simbolo letto: va scelta a mano.");
                return;
            }

            var symbol = result.CurrencySymbol.Trim();

            // Il passo che mancava. I simboli inequivocabili erano usati solo per RICONOSCERE un
            // simbolo nel testo e, piu' sotto, per leggerlo da un campo tipizzato Currency: sui
            // campi tipizzati Double - la maggioranza - il simbolo veniva letto correttamente e poi
            // buttato via, e una fattura in euro finiva con "valuta non rilevata".
            if (UnambiguousCurrencySymbols.TryGetValue(symbol, out var certain))
            {
                result.Currency = certain;
                _logger.LogInformation("Simbolo '{Symbol}' inequivocabile: valuta {Currency}.", symbol, certain);
                return;
            }

            if (!AmbiguousCurrencySymbols.TryGetValue(symbol, out var candidates))
            {
                _logger.LogWarning("Simbolo valuta '{Symbol}' non riconosciuto: la valuta va scelta a mano.", symbol);
                return;
            }

            var ordered = candidates.ToList();
            var hinted = GuessCurrencyFromAddress(result.MerchantAddress);

            if (hinted != null && ordered.Remove(hinted))
                ordered.Insert(0, hinted);

            result.CurrencyCandidates = ordered;

            _logger.LogInformation(
                "Simbolo '{Symbol}' ambiguo: propongo {Candidates} (indirizzo: {Hint}).",
                symbol, string.Join(", ", ordered), hinted ?? "nessun indizio");
        }

        private string GuessCurrencyFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;

            var normalized = address.ToUpperInvariant();

            foreach (var (pattern, currency) in AddressCurrencyHints)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(normalized, pattern))
                    return currency;
            }

            return null;
        }

        /// <summary>
        /// Simbolo della valuta, cercato PRIMA nel campo strutturato e poi nel testo grezzo.
        /// <para>
        /// Il testo grezzo si legge qualunque sia il tipo del campo, ed e' la parte che conta:
        /// su questi scontrini Azure spesso non tipizza l'importo come <c>Currency</c> ma come
        /// <c>Double</c>, o lo lascia ricavare dal contenuto. Finche' questa ricerca stava dietro
        /// un <c>FieldType == Currency</c>, il "$" scritto sulla carta veniva ignorato — mentre
        /// <see cref="SafeGetCurrencyAmount"/>, due metodi piu' sotto, lo incontra e lo cancella
        /// con una Replace per poter leggere il numero.
        /// </para>
        /// </summary>
        private string TryGetCurrencySymbol(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;

                if (field.FieldType == DocumentFieldType.Currency)
                {
                    try
                    {
                        var symbol = field.Value.AsCurrency().Symbol;
                        if (!string.IsNullOrWhiteSpace(symbol))
                            return symbol.Trim();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Impossibile leggere il simbolo valuta dal campo '{Name}'", name);
                    }
                }

                // Vale per QUALUNQUE tipo di campo: e' quello che c'e' scritto sullo scontrino.
                var fromContent = ExtractSymbolFromText(field.Content);
                if (fromContent != null)
                {
                    _logger.LogInformation("Simbolo '{Symbol}' letto dal contenuto del campo '{Name}' (tipo {Type}).",
                        fromContent, name, field.FieldType);
                    return fromContent;
                }
            }

            return null;
        }

        private static string ExtractSymbolFromText(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            foreach (var symbol in UnambiguousCurrencySymbols.Keys)
            {
                if (content.Contains(symbol, StringComparison.Ordinal))
                    return symbol;
            }

            foreach (var symbol in AmbiguousCurrencySymbols.Keys)
            {
                if (content.Contains(symbol, StringComparison.Ordinal))
                    return symbol;
            }

            return null;
        }

        /// <summary>
        /// Registra cosa ha davvero restituito il servizio sui campi monetari.
        /// <para>
        /// <c>RawFields</c> era dichiarato e mai popolato: quando si e' voluto capire perche' la
        /// valuta risultasse sempre EUR, il dato salvato non permetteva di distinguere "estratta"
        /// da "ripiegata sul default" e la risposta si e' dovuta dedurre dal codice. Con questo,
        /// la prossima volta la domanda si chiude guardando la riga.
        /// </para>
        /// </summary>
        private void FillCurrencyDiagnostics(
            IReadOnlyDictionary<string, DocumentField> fields,
            ReceiptExtractionResult result)
        {
            // Si registra OGNI campo trovato, non solo quelli di tipo Currency. Filtrando per tipo,
            // la diagnostica aveva lo stesso punto cieco del codice che doveva spiegare: su uno
            // scontrino in dollari risultava vuota, e sembrava che Azure non avesse restituito
            // niente, mentre semplicemente l'importo non era tipizzato come Currency.
            foreach (var name in new[] { "Total", "InvoiceTotal", "AmountDue", "TotalAmount", "Subtotal", "TotalTax" })
            {
                if (!fields.TryGetValue(name, out var field)) continue;

                string code = null;
                string symbol = null;

                if (field.FieldType == DocumentFieldType.Currency)
                {
                    try
                    {
                        var currency = field.Value.AsCurrency();
                        code = currency.Code;
                        symbol = currency.Symbol;
                    }
                    catch (Exception ex)
                    {
                        result.RawFields[name + ".error"] = ex.Message;
                    }
                }

                result.RawFields[name] = new
                {
                    FieldType = field.FieldType.ToString(),
                    Code = code,
                    Symbol = symbol,
                    Content = field.Content,
                    Confidence = field.Confidence
                };
            }
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

                // Quantity -> can be Double, Int64 or String
                if (dict.TryGetValue("Quantity", out var qtyF))
                    lineItem.Quantity = SafeGetDecimal(qtyF);

                // Unit price
                if (dict.TryGetValue("UnitPrice", out var upF))
                    lineItem.UnitPrice = SafeGetCurrencyAmount(upF);

                // Total price -> "TotalPrice" in receipt, "Amount" in invoice
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

        /// <summary>
        /// Simboli che identificano una valuta senza ambiguita': qui si puo' concludere da soli.
        /// </summary>
        private static readonly Dictionary<string, string> UnambiguousCurrencySymbols = new()
        {
            ["€"] = "EUR",
            ["£"] = "GBP",
            ["₣"] = "CHF",
            ["₹"] = "INR",
            ["₽"] = "RUB",
            ["₩"] = "KRW",
            ["₪"] = "ILS",
            ["₺"] = "TRY",
            ["zł"] = "PLN",
            ["Kč"] = "CZK",
        };

        /// <summary>
        /// Simboli usati da piu' valute, con i candidati in ordine di probabilita'.
        /// <para>
        /// Non si sceglie per conto dell'operatore - una valuta sbagliata falsa la conversione e
        /// quindi il rimborso - ma non si butta via nemmeno l'informazione: il simbolo sullo
        /// scontrino c'e', e lasciare il campo vuoto dicendo "valuta non rilevata" mentre il "$"
        /// e' bello visibile e' un modo di dare torto a chi guarda.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, string[]> AmbiguousCurrencySymbols = new()
        {
            ["$"] = new[] { "USD", "CAD", "AUD", "NZD", "SGD", "HKD", "MXN" },
            ["¥"] = new[] { "JPY", "CNY" },
            ["kr"] = new[] { "SEK", "NOK", "DKK" },
            ["R$"] = new[] { "BRL" },
            ["₨"] = new[] { "PKR", "LKR", "NPR" },
        };

        /// <summary>
        /// Indizi nell'indirizzo dell'esercente che permettono di mettere in cima il candidato
        /// giusto. Non decide: riordina soltanto una lista che l'operatore conferma comunque.
        /// </summary>
        private static readonly (string Pattern, string Currency)[] AddressCurrencyHints =
        {
            (@"\b(USA|U\.S\.A\.|UNITED STATES)\b", "USD"),
            // Sigla di stato USA di due lettere seguita dal CAP a 5 cifre: "Redmond, WA 98052".
            (@"\b[A-Z]{2}\s+\d{5}(-\d{4})?\b", "USD"),
            (@"\b(CANADA)\b", "CAD"),
            (@"\b(AUSTRALIA)\b", "AUD"),
            (@"\b(SINGAPORE)\b", "SGD"),
            (@"\b(HONG KONG)\b", "HKD"),
            (@"\b(JAPAN|NIPPON)\b", "JPY"),
            (@"\b(CHINA)\b", "CNY"),
        };

        /// <summary>
        /// Ricava il codice valuta ISO dai campi estratti.
        /// <para>
        /// Prima passata su tutti i campi cercando il codice ISO vero; solo se nessuno ce l'ha si
        /// ripiega sul simbolo. Prima si usciva al primo campo di tipo Currency restituendone il
        /// <c>Code</c> anche quando era null — e Azure lo popola di rado, mentre il simbolo lo
        /// restituisce quasi sempre — cosi' gli altri campi non venivano mai provati e il
        /// chiamante ripiegava su "EUR" per qualunque scontrino del mondo.
        /// </para>
        /// </summary>
        private string TryGetCurrencyCode(
            IReadOnlyDictionary<string, DocumentField> fields,
            params string[] names)
        {
            string symbolFallback = null;

            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;
                if (field.FieldType != DocumentFieldType.Currency) continue;

                try
                {
                    var currency = field.Value.AsCurrency();

                    if (!string.IsNullOrWhiteSpace(currency.Code))
                        return currency.Code.Trim().ToUpperInvariant();

                    // Il codice manca: si tiene da parte il simbolo e si continua a cercare,
                    // magari un altro campo il codice ce l'ha.
                    if (symbolFallback == null
                        && !string.IsNullOrWhiteSpace(currency.Symbol)
                        && UnambiguousCurrencySymbols.TryGetValue(currency.Symbol.Trim(), out var fromSymbol))
                    {
                        symbolFallback = fromSymbol;
                    }
                    else if (!string.IsNullOrWhiteSpace(currency.Symbol))
                    {
                        _logger.LogInformation(
                            "Simbolo valuta '{Symbol}' ambiguo sul campo '{Name}': la valuta resta da confermare.",
                            currency.Symbol, name);
                    }
                }
                catch (Exception ex)
                {
                    // Prima era un catch muto: quando l'estrazione falliva non lo sapeva nessuno.
                    _logger.LogDebug(ex, "Impossibile leggere la valuta dal campo '{Name}'", name);
                }
            }

            if (symbolFallback != null)
                return symbolFallback;

            // Ultima strada, e sulle fatture e' quella buona: il codice scritto per esteso accanto
            // all'importo ("12,20 EUR"). Non c'e' nessun simbolo da leggere, quindi tutta la logica
            // sui simboli non trova niente - ed e' il motivo per cui una fattura in euro risultava
            // senza valuta pur avendo "EUR" stampato sopra.
            foreach (var name in names)
            {
                if (!fields.TryGetValue(name, out var field)) continue;

                var fromText = ExtractIsoCodeFromText(field.Content);
                if (fromText == null) continue;

                _logger.LogInformation("Codice valuta '{Code}' letto dal contenuto del campo '{Name}'.", fromText, name);
                return fromText;
            }

            return null;
        }

        /// <summary>
        /// Cerca un codice ISO scritto nel testo, accettando solo quelli noti: un confronto su tre
        /// lettere maiuscole qualunque prenderebbe per valuta anche "IVA" o una sigla dell'azienda.
        /// </summary>
        private static string ExtractIsoCodeFromText(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return null;

            foreach (Match match in IsoCodeInText.Matches(content.ToUpperInvariant()))
            {
                var code = match.Value;
                if (KnownCurrencyCodes.Contains(code))
                    return code;
            }

            return null;
        }

        private static readonly Regex IsoCodeInText =
            new(@"\b[A-Z]{3}\b", RegexOptions.Compiled);

        /// <summary>
        /// Codici accettati quando compaiono nel testo. Sono quelli delle due tabelle dei simboli
        /// piu' le valute senza un simbolo proprio, che nei documenti si scrivono sempre cosi'.
        /// </summary>
        private static readonly HashSet<string> KnownCurrencyCodes = new(StringComparer.Ordinal)
        {
            "EUR", "USD", "GBP", "CHF", "JPY", "CNY", "INR", "RUB", "KRW", "ILS", "TRY",
            "PLN", "CZK", "SEK", "NOK", "DKK", "BRL", "CAD", "AUD", "NZD", "SGD", "HKD",
            "MXN", "ZAR", "HUF", "RON", "BGN", "HRK", "AED", "SAR", "THB", "MYR", "IDR",
            "PHP", "VND", "ARS", "CLP", "COP", "PEN", "UYU", "EGP", "MAD", "TND", "PKR",
            "LKR", "NPR", "ISK", "UAH", "RSD", "TWD"
        };

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
        /// Reads the raw Content property -> works for PhoneNumber, Address, etc.
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
                // Si tiene solo cio' che compone un numero e si butta tutto il resto: simboli di
                // qualunque valuta, spazi, sigle. Prima i simboli erano elencati a mano - e due
                // dei tre erano caratteri corrotti nel sorgente, quindi "€" e "£" non venivano
                // rimossi e un importo europeo che arrivava fin qui non si lasciava leggere.
                var raw = System.Text.RegularExpressions.Regex.Replace(field.Content, @"[^\d.,\-]", "");

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
