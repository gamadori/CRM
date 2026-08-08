using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CRM.Server.Services.Usage;
using CRM.Shared;

namespace CRM.Server.Services.ExpenseCategorization
{
    /// <summary>
    /// Ultimo livello: si chiede al modello che spesa e', ma solo per i documenti su cui le
    /// regole non hanno saputo dire niente. Come gli altri servizi AI dell'applicazione tiene un
    /// client <b>nullable</b>: senza chiave API resta inerte invece di far fallire il
    /// caricamento di uno scontrino.
    /// <para>
    /// I documenti si mandano tutti in una chiamata sola. Un PDF di trasferta ne contiene dieci,
    /// e dieci chiamate costerebbero dieci volte tanto per rispondere alla stessa domanda.
    /// </para>
    /// </summary>
    public class ExpenseCategoryAiClient : IExpenseCategoryAiClient
    {
        /// <summary>Oltre questa lunghezza la descrizione di una riga non aiuta a decidere.</summary>
        private const int MaxLineChars = 120;

        /// <summary>Righe per documento oltre le quali si ripete solo lo stesso indizio.</summary>
        private const int MaxLinesPerDocument = 12;

        private readonly ILogger<ExpenseCategoryAiClient> _logger;
        private readonly IUsageRecorder _usage;
        private readonly AnthropicClient? _client;

        public ExpenseCategoryAiClient(IConfiguration configuration, ILogger<ExpenseCategoryAiClient> logger, IUsageRecorder usage)
        {
            _logger = logger;
            _usage = usage;

            var apiKey = configuration["Anthropic:ApiKey"];
            Model = configuration["Anthropic:ChatModel"] ?? "claude-opus-4-8";

            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_ANTHROPIC_API_KEY_HERE")
                _client = new AnthropicClient { ApiKey = apiKey };
        }

        public bool IsAvailable => _client != null;

        public string Model { get; }

        public async Task<IReadOnlyList<ExpenseCategorySuggestion>?> SuggestAsync(
            IReadOnlyList<ExpenseCategoryRequest> requests,
            CancellationToken ct = default)
        {
            if (_client == null || requests.Count == 0)
                return null;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = Model,
                    MaxTokens = 300 + (150 * requests.Count),
                    System = BuildSystemPrompt(),
                    OutputConfig = new OutputConfig { Effort = Effort.Medium },
                    Messages = new List<MessageParam>
                    {
                        new() { Role = Role.User, Content = BuildUserPrompt(requests) }
                    },
                });

                // Una riga sola anche se i documenti erano dieci: la chiamata e' una, e il
                // costo per documento si ricava dividendo, non moltiplicando le righe.
                await _usage.RecordTokensAsync(
                    ExternalServiceFeature.ExpenseCategory, Model, response.TokenUsageOf(),
                    true, stopwatch.ElapsedMilliseconds);

                var text = string.Concat(
                    response.Content
                        .Select(x => x.Value)
                        .OfType<TextBlock>()
                        .Select(x => x.Text));

                return Parse(text, requests.Count);
            }
            catch (Exception ex)
            {
                // Non bloccante: la spesa si registra comunque, con la tipologia da indicare.
                _logger.LogWarning(ex, "Tipologia della nota spese non proposta dall'AI");
                return null;
            }
        }

        private static string BuildSystemPrompt()
        {
            var categories = new StringBuilder();
            foreach (var category in ExpenseCategories.All)
                categories.AppendLine($"- {category}: {ExpenseCategories.Hint(category)}");

            return $$"""
                Classifichi note spese aziendali: leggi i dati letti da uno scontrino o da una
                fattura e dici a quale voce di rimborso appartiene la spesa.

                VOCI DISPONIBILI (usa esattamente questi nomi):
                {{categories}}
                REGOLE:
                - Scegli SOLO tra le voci elencate. Se i dati non bastano a decidere, restituisci
                  "category": null: la voce verra' chiesta alla persona, ed e' molto meglio di una
                  voce sbagliata, perche' la tipologia determina la deducibilita' fiscale.
                - Non scegliere "Entertainment" (rappresentanza) a meno che il documento lo dica
                  esplicitamente: la differenza fra un pranzo di lavoro e uno di rappresentanza sta
                  nell'occasione, che sullo scontrino non c'e'.
                - Non usare "Other" come ripiego: se non sai, la risposta e' null.
                - "confidence" e' la tua confidenza reale, tra 0 e 1. Usa valori alti solo quando
                  l'esercente o le righe sono espliciti; se stai deducendo per analogia, resta
                  sotto 0.7.
                - "reason" spiega la scelta in una frase breve, in italiano, citando l'elemento
                  decisivo.
                - Rispondi con UN OGGETTO per ogni documento ricevuto, nello stesso ordine e con lo
                  stesso "index".
                - Rispondi ESCLUSIVAMENTE con un array JSON valido, senza testo prima o dopo e
                  senza blocchi markdown.

                SCHEMA:
                [{"index": 1, "category": "Meals", "confidence": 0.0, "reason": ""}]
                """;
        }

        private static string BuildUserPrompt(IReadOnlyList<ExpenseCategoryRequest> requests)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"DOCUMENTI DA CLASSIFICARE: {requests.Count}");

            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];

                sb.AppendLine();
                sb.AppendLine($"--- Documento {i + 1} ---");
                sb.AppendLine($"Esercente: {Value(request.MerchantName)}");
                sb.AppendLine($"Tipo rilevato: {Value(request.DocumentType)}");

                if (request.TotalAmount.HasValue)
                    sb.AppendLine($"Importo: {request.TotalAmount.Value:0.00} {request.Currency ?? string.Empty}".TrimEnd());

                if (!string.IsNullOrWhiteSpace(request.Description))
                    sb.AppendLine($"Descrizione: {request.Description.Trim()}");

                var lines = (request.Lines ?? Array.Empty<string>())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(MaxLinesPerDocument)
                    .ToList();

                if (lines.Count == 0)
                {
                    sb.AppendLine("Righe: nessuna");
                    continue;
                }

                sb.AppendLine("Righe:");
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > MaxLineChars)
                        trimmed = trimmed.Substring(0, MaxLineChars);

                    sb.AppendLine($"- {trimmed}");
                }
            }

            return sb.ToString();
        }

        private static string Value(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "non rilevato" : value.Trim();

        /// <summary>
        /// Estrae l'array JSON (tollerante a eventuali fence markdown) e lo rimette in ordine per
        /// indice. Una risposta che non copre tutti i documenti non e' un errore: i documenti
        /// mancanti restano semplicemente senza tipologia.
        /// </summary>
        private IReadOnlyList<ExpenseCategorySuggestion>? Parse(string? text, int expected)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("Tipologia AI: risposta senza array JSON riconoscibile");
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(text.Substring(start, end - start + 1));
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    return null;

                var suggestions = Enumerable.Repeat(ExpenseCategorySuggestion.None, expected).ToArray();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var index = GetInt(element, "index");
                    if (index == null || index < 1 || index > expected)
                        continue;

                    var category = ExpenseCategories.Parse(GetString(element, "category"));
                    if (category == null)
                        continue;

                    suggestions[index.Value - 1] = new ExpenseCategorySuggestion(
                        category,
                        GetDouble(element, "confidence"),
                        GetString(element, "reason"),
                        ExpenseCategorySource.Ai);
                }

                return suggestions;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Tipologia AI: JSON non valido");
                return null;
            }
        }

        private static int? GetInt(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                ? i
                : null;

        private static double GetDouble(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
                ? Math.Clamp(d, 0, 1)
                : 0;

        private static string? GetString(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
