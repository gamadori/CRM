using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using CRM.Server.Services.Usage;
using CRM.Shared;
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
        private readonly IUsageRecorder _usage;
        private readonly DocumentAnalysisClient _formRecognizerClient;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public AzureReceiptAnalyzer(
            ILogger<AzureReceiptAnalyzer> logger,
            IConfiguration configuration,
            IUsageRecorder usage)
        {
            _logger = logger;
            _usage = usage;

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
            string customModelId = null,
            ReceiptDocumentKind kind = ReceiptDocumentKind.Unknown)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (_formRecognizerClient == null)
                    return NotConfigured(stopwatch);

                if (useCustomModel && !string.IsNullOrEmpty(customModelId))
                {
                    var custom = await AnalyzeWithModelAsync(fileBytes, fileName, customModelId);
                    return Finish(custom, stopwatch, fileName);
                }

                var reading = IsPdf(fileName)
                    ? await AnalyzePdfAsync(fileBytes, fileName, kind)
                    : await AnalyzeImageAsync(fileBytes, fileName);

                if (reading.Documents.Count == 0)
                {
                    return Error(
                        "Nessun documento rilevato. Assicurati che lo scontrino/fattura sia leggibile e non ruotato.",
                        stopwatch);
                }

                return Finish(reading, stopwatch, fileName);
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

        private const string ReceiptModel = "prebuilt-receipt";
        private const string InvoiceModel = "prebuilt-invoice";

        /// <summary>
        /// Il risultato di un'analisi: i documenti riconosciuti e la valuta che compare nel
        /// <b>testo</b> del file.
        /// <para>
        /// La seconda parte esiste perche' su un conto d'albergo la valuta e' scritta dove i campi
        /// strutturati non arrivano: "Balance 0.00 <b>USD</b>" in fondo, "Rate: <b>$</b>127.00" in
        /// testata. Nessuno dei due sta nel campo Total, che contiene "104.92" e basta - quindi
        /// tutta la logica sulla valuta, che guarda solo i campi degli importi, non trovava niente
        /// e il documento risultava "valuta non rilevata" pur avendola stampata sopra due volte.
        /// </para>
        /// </summary>
        private sealed record Reading(
            IReadOnlyList<AnalyzedDocument> Documents,
            string TextCode,
            string TextSymbol)
        {
            public static Reading Empty { get; } = new(Array.Empty<AnalyzedDocument>(), null, null);

            public Reading With(IReadOnlyList<AnalyzedDocument> documents) =>
                new(documents, TextCode, TextSymbol);
        }

        private static bool IsPdf(string fileName)
            => !string.IsNullOrWhiteSpace(fileName)
               && Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Un'immagine e' uno scontrino fotografato: <c>prebuilt-receipt</c>. Se non ne cava
        /// niente si prova comunque il modello fattura - la foto di una fattura esiste.
        /// </summary>
        private async Task<Reading> AnalyzeImageAsync(byte[] fileBytes, string fileName)
        {
            var reading = await AnalyzeWithModelAsync(fileBytes, fileName, ReceiptModel);
            if (CountRealExpenses(reading.Documents) > 0)
                return reading;

            _logger.LogInformation(
                "'{FileName}': nessuna spesa leggibile con {Receipt}, riprovo con {Invoice}.",
                fileName, ReceiptModel, InvoiceModel);

            var asInvoice = await AnalyzeWithModelAsync(fileBytes, fileName, InvoiceModel);
            return CountRealExpenses(asInvoice.Documents) > 0 ? asInvoice : reading;
        }

        /// <summary>
        /// Dentro un PDF ci sono due cose diverse e indistinguibili dall'esterno: una fattura
        /// emessa, che <c>prebuilt-invoice</c> tratta come un documento solo anche su piu' pagine e
        /// di cui legge il codice valuta; e una scansione di scontrini, che <c>prebuilt-receipt</c>
        /// separa uno per uno mentre il modello fattura ne fonde due in uno perdendo il secondo
        /// importo - misurato su un PDF di due scontrini, 77,58 al posto di 77,58 + 6,60.
        /// <para>
        /// Se chi carica lo <b>dichiara</b>, si esegue solo il modello che serve: un'analisi invece
        /// di due. Se non lo dichiara si eseguono entrambi e vince il migliore - corretto, ma al
        /// doppio del costo. In ogni caso, se il modello scelto non cava niente si prova l'altro:
        /// una tendina sbagliata non deve lasciare nessuno senza estrazione.
        /// </para>
        /// </summary>
        private async Task<Reading> AnalyzePdfAsync(
            byte[] fileBytes, string fileName, ReceiptDocumentKind kind)
        {
            if (kind != ReceiptDocumentKind.Unknown)
                return await AnalyzeDeclaredPdfAsync(fileBytes, fileName, kind);

            var asInvoice = await AnalyzeWithModelAsync(fileBytes, fileName, InvoiceModel);
            var asReceipt = await AnalyzeWithModelAsync(fileBytes, fileName, ReceiptModel);

            var invoiceCount = CountRealExpenses(asInvoice.Documents);
            var receiptCount = CountRealExpenses(asReceipt.Documents);

            // Vince chi trova piu' spese vere. A parita' vince la fattura: legge il codice valuta
            // dove il modello scontrino lascia il campo vuoto, e su piu' pagine tiene unito.
            var useReceipt = receiptCount > invoiceCount;

            _logger.LogInformation(
                "'{FileName}': {Invoice} trova {InvoiceCount} spese, {Receipt} ne trova {ReceiptCount}. Uso {Chosen}.",
                fileName, InvoiceModel, invoiceCount, ReceiptModel, receiptCount,
                useReceipt ? ReceiptModel : InvoiceModel);

            return useReceipt ? asReceipt : asInvoice;
        }

        /// <summary>
        /// PDF dichiarato: si esegue il modello corrispondente e basta. L'altro entra in gioco solo
        /// se il primo non trova nessuna spesa - la dichiarazione fa risparmiare una chiamata, non
        /// toglie la rete di sicurezza.
        /// </summary>
        private async Task<Reading> AnalyzeDeclaredPdfAsync(
            byte[] fileBytes, string fileName, ReceiptDocumentKind kind)
        {
            var declared = kind == ReceiptDocumentKind.Invoice ? InvoiceModel : ReceiptModel;
            var other = declared == InvoiceModel ? ReceiptModel : InvoiceModel;

            var reading = await AnalyzeWithModelAsync(fileBytes, fileName, declared);

            if (CountRealExpenses(reading.Documents) > 0)
            {
                _logger.LogInformation(
                    "'{FileName}': dichiarato come {Kind}, usato {Model} senza seconda analisi.",
                    fileName, kind, declared);

                return reading;
            }

            _logger.LogInformation(
                "'{FileName}': dichiarato come {Kind} ma {Model} non trova spese, riprovo con {Other}.",
                fileName, kind, declared, other);

            var fallback = await AnalyzeWithModelAsync(fileBytes, fileName, other);
            return CountRealExpenses(fallback.Documents) > 0 ? fallback : reading;
        }

        /// <summary>
        /// Quante spese vere contiene il risultato: documenti con un importo totale proprio. E' il
        /// metro con cui si confrontano i due modelli, ed e' lo stesso con cui poi si scartano i
        /// frammenti - vedi <see cref="KeepOnlyRealExpenses"/>.
        /// </summary>
        private int CountRealExpenses(IReadOnlyList<AnalyzedDocument> documents)
            => documents.Count(document =>
                TryGetCurrency(document.Fields, out _, "Total", "InvoiceTotal", "AmountDue", "TotalAmount")
                    .HasValue);

        /// <summary>
        /// Analisi con un modello: documento intero piu' le eventuali pagine che Azure non ha
        /// attribuito a nessun documento, rianalizzate singolarmente.
        /// </summary>
        private async Task<Reading> AnalyzeWithModelAsync(
            byte[] fileBytes, string fileName, string modelId)
        {
            _logger.LogInformation("Elaborazione file '{FileName}' ({Bytes} bytes) con modello '{ModelId}'",
                fileName, fileBytes.Length, modelId);

            using var stream = new MemoryStream(fileBytes);
            var analysisStopwatch = Stopwatch.StartNew();
            var operation = await _formRecognizerClient.AnalyzeDocumentAsync(
                WaitUntil.Completed, modelId, stream);

            var result = operation.Value;

            // Azure fattura a PAGINA, non a documento: le unita' sono le pagine di questa
            // analisi. Il ripiego sull'altro modello e la rianalisi pagina per pagina piu' sotto
            // sono chiamate a se', e infatti si registrano a se': sono spesa vera in piu'.
            await _usage.RecordUnitsAsync(
                ExternalServiceProvider.Azure, ExternalServiceFeature.ReceiptOcr, modelId,
                Math.Max(1, result.Pages.Count), true, analysisStopwatch.ElapsedMilliseconds);

            _logger.LogInformation("Analisi completata con '{ModelId}'. Documenti trovati: {Count}",
                modelId, result.Documents.Count);

            var documents = (IReadOnlyList<AnalyzedDocument>)result.Documents;

            var coveredPages = result.Documents
                .SelectMany(document => document.BoundingRegions)
                .Select(region => region.PageNumber)
                .Distinct()
                .ToHashSet();
            var missingPages = Enumerable.Range(1, result.Pages.Count)
                .Where(pageNumber => !coveredPages.Contains(pageNumber))
                .ToList();

            // Se Azure ha riconosciuto un documento che copre tutte le pagine, e' una fattura
            // multipagina e resta unita. Si analizzano singolarmente solo le pagine non attribuite
            // ad alcun documento fiscale.
            if (missingPages.Count > 0)
            {
                var pageDocuments = await AnalyzePdfPagesAsync(fileBytes, modelId, missingPages);
                if (pageDocuments.Count > 0)
                    documents = documents.Concat(pageDocuments).ToList();
            }

            if (documents.Count == 0)
            {
                _logger.LogWarning(
                    "Nessun documento strutturato rilevato con '{ModelId}'. Pagine: {Pages}, Parole: {Words}",
                    modelId, result.Pages.Count, result.Pages.Sum(p => p.Words.Count));
            }

            var (textCode, textSymbol) = ReadCurrencyFromText(result.Content);

            if (textCode != null || textSymbol != null)
            {
                _logger.LogInformation(
                    "'{FileName}': nel testo del documento compaiono codice='{Code}' simbolo='{Symbol}'.",
                    fileName, textCode ?? "-", textSymbol ?? "-");
            }

            return new Reading(documents, textCode, textSymbol);
        }

        private ReceiptExtractionResult Finish(Reading reading, Stopwatch stopwatch, string fileName)
        {
            var extractionResult = ExtractDocuments(reading, stopwatch);

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

                    var pageStopwatch = Stopwatch.StartNew();
                    var pageOperation = await _formRecognizerClient.AnalyzeDocumentAsync(
                        WaitUntil.Completed,
                        modelId,
                        pageStream,
                        options);

                    var pageResult = pageOperation.Value;

                    await _usage.RecordUnitsAsync(
                        ExternalServiceProvider.Azure, ExternalServiceFeature.ReceiptOcr, modelId,
                        1, true, pageStopwatch.ElapsedMilliseconds);

                    if (pageResult.Documents.Count == 0)
                    {
                        _logger.LogWarning("Pagina {PageNumber}: nessun documento fiscale rilevato.", pageNumber);
                        continue;
                    }

                    // I frammenti si scartano in un punto solo, dove tutti i documenti si
                    // ritrovano: vedi ExtractDocuments.
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

        /// <summary>
        /// Tiene solo i documenti che sono davvero una spesa a se': quelli con un importo totale
        /// proprio.
        /// <para>
        /// Un documento senza totale non e' una spesa, e' la <b>continuazione</b> di quello prima -
        /// le righe di una fattura, le condizioni di pagamento, un allegato. Azure lo restituisce
        /// come documento separato perche', guardando quella pagina da sola, non ha modo di sapere
        /// che appartiene a un documento piu' grande; noi invece lo sappiamo, perche' una spesa
        /// senza importo non esiste.
        /// </para>
        /// <para>
        /// Il filtro sbaglia per difetto apposta: unire due spese in una si nota subito, il totale
        /// non torna e si rimedia. Una nota spese inventata invece finisce nei rimborsi e non se ne
        /// accorge nessuno. Per lo stesso motivo, se nessun documento ha un totale non si scarta
        /// niente: meglio farli vedere tutti e lasciar decidere, che restare senza niente.
        /// </para>
        /// </summary>
        private List<ReceiptExtractionResult> KeepOnlyRealExpenses(List<ReceiptExtractionResult> documents)
        {
            var withTotal = documents.Where(d => d.TotalAmount.HasValue).ToList();

            if (withTotal.Count == 0 || withTotal.Count == documents.Count)
                return documents;

            foreach (var discarded in documents.Where(d => !d.TotalAmount.HasValue))
            {
                _logger.LogInformation(
                    "Documento a pagina {Page} senza importo totale: e' la continuazione del precedente, non una nota spese a se'. Scartato.",
                    discarded.PageFrom);
            }

            return withTotal;
        }

        private ReceiptExtractionResult ExtractDocuments(Reading reading, Stopwatch stopwatch)
        {
            var documents = reading.Documents;

            if (documents.Count == 1)
                return ExtractDocument(documents[0], stopwatch.ElapsedMilliseconds, reading);

            _logger.LogInformation(
                "PDF multi-documento rilevato: aggrego {Count} documenti restituiti da Azure.",
                documents.Count);

            var partialResults = documents
                .Select(document => ExtractDocument(document, stopwatch.ElapsedMilliseconds, reading))
                .OrderBy(document => document.PageFrom ?? int.MaxValue)
                .ToList();

            partialResults = KeepOnlyRealExpenses(partialResults);

            // Scartati tutti tranne uno: non c'e' piu' niente da aggregare, ed e' il caso della
            // fattura multipagina che Azure aveva spezzato.
            if (partialResults.Count == 1)
            {
                partialResults[0].ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return partialResults[0];
            }

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
                CurrencyHint = FirstNonEmpty(partialResults.Select(x => x.CurrencyHint)),
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

        private ReceiptExtractionResult ExtractDocument(
            AnalyzedDocument document, long processingTimeMs, Reading reading)
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

            ExtractCommonFields(document, extractionResult, reading);
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
        private void ExtractCommonFields(
            AnalyzedDocument document, ReceiptExtractionResult result, Reading reading)
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
            // Ogni tipo di scontrino chiama la data a modo suo: un conto d'albergo
            // (receipt.hotel) non ha TransactionDate, ha ArrivalDate e DepartureDate, e cercando
            // solo i nomi degli altri tipi la data si perdeva pur essendo stata letta da Azure.
            // Fra le due dell'albergo viene prima la partenza: il conto si salda al check-out.
            // DueDate resta per ultima perche' e' la scadenza di pagamento, cioe' il surrogato
            // peggiore della data di spesa.
            result.TransactionDate = TryGetDate(f,
                "TransactionDate", "InvoiceDate", "DepartureDate", "ArrivalDate", "ServiceDate", "DueDate");

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

            // La valuta si risolve PRIMA della descrizione: la descrizione la stampa, e costruirla
            // prima significava scrivere "104,92" senza valuta anche quando poi la valuta veniva
            // riconosciuta un istante dopo.
            ResolveCurrencyCandidates(result, reading);
            FillCurrencyDiagnostics(f, result);

            // ?? DESCRIZIONE AUTO-GENERATA ?????????????????????????????????????????????
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(result.MerchantName)) parts.Add(result.MerchantName);
            if (result.TransactionDate.HasValue) parts.Add(result.TransactionDate.Value.ToString("dd/MM/yyyy"));
            if (result.TotalAmount.HasValue) parts.Add(FormatAmount(result));
            result.Description = string.Join(" - ", parts);
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
        private void ResolveCurrencyCandidates(ReceiptExtractionResult result, Reading reading)
        {
            if (!string.IsNullOrWhiteSpace(result.Currency))
                return;

            // Il codice ISO scritto nel TESTO del documento, fuori dai campi degli importi. Su un
            // conto d'albergo e' l'unico posto in cui compare ("Balance 0.00 USD"), e vale quanto
            // il codice accanto al totale: e' scritto sulla carta, non dedotto da Azure - che e'
            // la differenza con il campo "Currency", che invece resta ignorato (vedi sotto).
            if (!string.IsNullOrWhiteSpace(reading?.TextCode))
            {
                result.Currency = reading.TextCode;
                result.CurrencyHint = $"Codice {reading.TextCode} letto sul documento.";
                _logger.LogInformation(
                    "Valuta {Currency} letta dal testo del documento (nessun campo strutturato la conteneva).",
                    reading.TextCode);
                return;
            }

            // Nessun simbolo nei campi degli importi: si guarda quello scritto nel resto del
            // documento ("Rate: $127.00" in testata di un conto d'albergo).
            if (string.IsNullOrWhiteSpace(result.CurrencySymbol)
                && !string.IsNullOrWhiteSpace(reading?.TextSymbol))
            {
                result.CurrencySymbol = reading.TextSymbol;
                _logger.LogInformation(
                    "Simbolo '{Symbol}' letto dal testo del documento, fuori dai campi degli importi.",
                    reading.TextSymbol);
            }

            if (string.IsNullOrWhiteSpace(result.CurrencySymbol))
            {
                ProposeCurrencyFromAddress(result);
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
                ProposeCurrencyFromAddress(result);
                return;
            }

            var ordered = candidates.ToList();
            var hinted = GuessCurrencyFromAddress(result.MerchantAddress);

            if (hinted != null && ordered.Remove(hinted))
                ordered.Insert(0, hinted);

            result.CurrencyCandidates = ordered;
            result.CurrencyHint = $"Sul documento c'è «{symbol}», che appartiene a più valute."
                + (hinted != null ? $" L'indirizzo dell'esercente indica {hinted}." : string.Empty);

            _logger.LogInformation(
                "Simbolo '{Symbol}' ambiguo: propongo {Candidates} (indirizzo: {Hint}).",
                symbol, string.Join(", ", ordered), hinted ?? "nessun indizio");
        }

        /// <summary>
        /// Ultimo indizio: il paese dell'esercente. Non decide - un indirizzo americano non e' una
        /// valuta scritta - ma proporre "USD" da confermare con un clic e' molto meglio di un
        /// campo vuoto con scritto "valuta non trovata" su un conto di Redmond, WA.
        /// </summary>
        private void ProposeCurrencyFromAddress(ReceiptExtractionResult result)
        {
            var hinted = GuessCurrencyFromAddress(result.MerchantAddress);

            if (hinted == null)
            {
                _logger.LogWarning("Valuta NON estratta e nessun indizio utilizzabile: va scelta a mano.");
                return;
            }

            result.CurrencyCandidates = new List<string> { hinted };
            result.CurrencyHint =
                $"La valuta non è scritta sul documento, ma l'indirizzo dell'esercente indica {hinted}.";

            _logger.LogInformation(
                "Valuta non scritta sul documento: dall'indirizzo '{Address}' propongo {Currency}.",
                result.MerchantAddress, hinted);
        }

        private static (string Code, string Symbol) ReadCurrencyFromText(string content)
            => ReceiptCurrencyText.Read(content);

        private static string GuessCurrencyFromAddress(string address)
            => ReceiptCurrencyText.GuessFromAddress(address);

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
            => ReceiptCurrencyText.ExtractSymbol(content);

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

        // Le tabelle di simboli, codici e indizi d'indirizzo stanno in ReceiptCurrencyText: sono
        // funzioni pure su stringhe, e li' si possono provare sul testo di un documento vero senza
        // chiamare Azure.
        private static Dictionary<string, string> UnambiguousCurrencySymbols => ReceiptCurrencyText.UnambiguousSymbols;

        private static Dictionary<string, string[]> AmbiguousCurrencySymbols => ReceiptCurrencyText.AmbiguousSymbols;

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

            // NON si legge il campo "Currency" che alcuni tipi di scontrino espongono. Sembra la
            // fonte piu' autorevole ed e' invece la piu' pericolosa: su un conto dell'Holiday Inn
            // di Bologna, con importi in euro e nessun simbolo estero, Azure lo valorizza a "USD".
            // Provato e tolto: preferiamo "valuta da indicare", che l'operatore vede e corregge,
            // a una valuta sbagliata con l'aria di essere giusta, che falsa la conversione e
            // quindi il rimborso senza che nessuno se ne accorga.
            return null;
        }

        /// <summary>
        /// Cerca un codice ISO scritto nel testo, accettando solo quelli noti: un confronto su tre
        /// lettere maiuscole qualunque prenderebbe per valuta anche "IVA" o una sigla dell'azienda.
        /// </summary>
        private static string ExtractIsoCodeFromText(string content)
            => ReceiptCurrencyText.ExtractIsoCode(content);

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
