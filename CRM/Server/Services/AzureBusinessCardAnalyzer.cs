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
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Legge i biglietti da visita con il modello <c>prebuilt-businessCard</c>, sullo stesso
    /// servizio Azure gia' usato per gli scontrini.
    /// <para>
    /// Non solleva mai: restituisce un risultato con <see cref="BusinessCardExtractionResult.Success"/>
    /// falso e il motivo. Allo stand un'eccezione significherebbe perdere la persona che hai davanti
    /// mentre guardi un messaggio d'errore; qui invece si compila a mano e si va avanti.
    /// </para>
    /// </summary>
    public class AzureBusinessCardAnalyzer : IBusinessCardAnalyzer
    {
        private const string BusinessCardModel = "prebuilt-businessCard";

        private readonly ILogger<AzureBusinessCardAnalyzer> _logger;
        private readonly IUsageRecorder _usage;
        private readonly DocumentAnalysisClient? _client;

        public AzureBusinessCardAnalyzer(ILogger<AzureBusinessCardAnalyzer> logger, IConfiguration configuration, IUsageRecorder usage)
        {
            _logger = logger;
            _usage = usage;

            var endpoint = configuration["AzureFormRecognizer:Endpoint"];
            var apiKey = configuration["AzureFormRecognizer:ApiKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                _logger.LogWarning("Azure Form Recognizer non configurato: i biglietti si compileranno a mano.");
            else
                _client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }

        public async Task<BusinessCardExtractionResult> AnalyzeAsync(byte[] fileBytes, string fileName)
        {
            var stopwatch = Stopwatch.StartNew();

            if (_client == null)
                return Fail("Lettura automatica non configurata: compila i campi a mano.", stopwatch);

            try
            {
                using var stream = new MemoryStream(fileBytes);
                var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, BusinessCardModel, stream);

                // Un biglietto e' una foto, una pagina: si paga anche quando non ne esce niente,
                // quindi si registra prima di controllare l'esito.
                await _usage.RecordUnitsAsync(
                    ExternalServiceProvider.Azure, ExternalServiceFeature.BusinessCardOcr, BusinessCardModel,
                    Math.Max(1, operation.Value.Pages.Count), true, stopwatch.ElapsedMilliseconds);

                var document = operation.Value.Documents.FirstOrDefault();

                if (document == null)
                    return Fail("Nessun biglietto riconosciuto nella foto: compila i campi a mano.", stopwatch);

                var fields = document.Fields;
                var result = new BusinessCardExtractionResult
                {
                    Success = true,
                    FullName = First(fields, "ContactNames"),
                    CompanyName = First(fields, "CompanyNames"),
                    JobTitle = First(fields, "JobTitles"),
                    Email = First(fields, "Emails"),

                    // I numeri stanno su tre campi diversi e su un biglietto ce n'e' spesso piu' di
                    // uno: si prende il primo disponibile dando la precedenza al cellulare, che e'
                    // quello che serve per richiamare chi hai conosciuto in fiera.
                    Phone = First(fields, "MobilePhones") ?? First(fields, "WorkPhones") ?? First(fields, "OtherPhones"),
                    Website = First(fields, "Websites"),
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };

                var confidences = fields.Values
                    .Where(f => f?.Confidence != null)
                    .Select(f => f.Confidence!.Value)
                    .ToList();

                if (confidences.Count > 0)
                    result.AverageConfidence = confidences.Average();

                if (!result.HasAnyField)
                    return Fail("Biglietto illeggibile: compila i campi a mano.", stopwatch);

                return result;
            }
            catch (RequestFailedException ex) when (ex.Status == 400)
            {
                _logger.LogWarning(ex, "Immagine non supportata per il biglietto '{FileName}'", fileName);
                return Fail("Immagine non supportata: usa una foto JPG o PNG.", stopwatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lettura del biglietto '{FileName}' non riuscita", fileName);
                return Fail("Lettura non riuscita: compila i campi a mano, la foto resta allegata.", stopwatch);
            }
        }

        /// <summary>
        /// Primo valore leggibile di un campo, sia che il modello lo restituisca come stringa sia
        /// come elenco (nomi, aziende e telefoni arrivano quasi sempre come elenchi).
        /// <para>
        /// Si legge <c>Content</c>, cioe' il testo com'e' scritto sul biglietto, invece di
        /// ricostruirlo dai sotto-campi tipizzati: su un nome straniero "Cognome Nome" ricomposto a
        /// mano esce spesso invertito, e sul cartoncino invece c'e' gia' nell'ordine giusto.
        /// </para>
        /// </summary>
        private static string? First(IReadOnlyDictionary<string, DocumentField> fields, string name)
        {
            if (!fields.TryGetValue(name, out var field) || field == null)
                return null;

            if (field.FieldType == DocumentFieldType.List)
            {
                try
                {
                    var first = field.Value.AsList().FirstOrDefault();
                    return Clean(first?.Content);
                }
                catch (InvalidOperationException)
                {
                    // Tipo diverso da quello atteso: si ripiega sul contenuto grezzo del campo.
                    return Clean(field.Content);
                }
            }

            return Clean(field.Content);
        }

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // I campi multiriga (indirizzi, nomi su due righe) arrivano con gli a capo dentro:
            // in un campo di un modulo diventano caratteri invisibili che sporcano le ricerche.
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private static BusinessCardExtractionResult Fail(string message, Stopwatch stopwatch)
            => new()
            {
                Success = false,
                ErrorMessage = message,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
    }
}
