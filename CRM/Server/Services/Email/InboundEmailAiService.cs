using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Implementazione basata su Claude (SDK Anthropic). Come <c>TicketSummaryService</c>, tiene un
    /// client <b>nullable</b>: senza API key il servizio resta inerte invece di far fallire l'avvio
    /// o la ricezione della posta.
    /// </summary>
    public class InboundEmailAiService : IInboundEmailAiService
    {
        /// <summary>Limite del corpo passato al modello: oltre non aggiunge informazione utile al triage.</summary>
        private const int MaxBodyChars = 6000;

        private readonly ILogger<InboundEmailAiService> _logger;
        private readonly AnthropicClient? _client;
        private readonly string _model;

        public InboundEmailAiService(IConfiguration configuration, ILogger<InboundEmailAiService> logger)
        {
            _logger = logger;

            var apiKey = configuration["Anthropic:ApiKey"];
            _model = configuration["Anthropic:ChatModel"] ?? "claude-opus-4-8";

            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_ANTHROPIC_API_KEY_HERE")
                _client = new AnthropicClient { ApiKey = apiKey };
        }

        public bool IsAvailable => _client != null;

        public async Task<InboundEmailTriage?> AnalyzeAsync(string subject, string? body, CancellationToken ct = default)
        {
            if (_client == null)
                return null;

            try
            {
                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = _model,
                    MaxTokens = 700,
                    System = BuildSystemPrompt(),
                    OutputConfig = new OutputConfig { Effort = Effort.Medium },
                    Messages = new List<MessageParam>
                    {
                        new() { Role = Role.User, Content = BuildUserPrompt(subject, body) }
                    },
                });

                var text = string.Concat(
                    response.Content
                        .Select(x => x.Value)
                        .OfType<TextBlock>()
                        .Select(x => x.Text));

                return Parse(text);
            }
            catch (Exception ex)
            {
                // Non bloccante: la posta continua a essere processata senza AI.
                _logger.LogWarning(ex, "Triage AI dell'email in arrivo non riuscito");
                return null;
            }
        }

        private static string BuildSystemPrompt()
        {
            return """
                Sei un sistema di triage della posta in arrivo di un'assistenza tecnica.
                Analizza l'email e decidi se è una richiesta di assistenza che merita l'apertura di un ticket.

                SONO richieste di assistenza: guasti, malfunzionamenti, anomalie, richieste di intervento
                o di ricambi, domande tecniche sull'uso di una macchina, reclami su un prodotto.

                NON sono richieste di assistenza: newsletter, comunicazioni commerciali, risposte automatiche
                (fuori sede/out-of-office), conferme di lettura, notifiche di sistema, spam, posta amministrativa
                (fatture, solleciti), messaggi di sola cortesia.

                REGOLE:
                - Rispondi ESCLUSIVAMENTE con un oggetto JSON valido, senza testo prima o dopo, senza blocchi markdown.
                - Non inventare fatti: usa solo quanto scritto nell'email.
                - "summary" deve essere un riassunto operativo in italiano, utile a un tecnico per capire subito
                  il problema: cosa succede, su cosa, con quali sintomi. Niente markdown, massimo 600 caratteri.
                - "title" è un titolo sintetico, massimo 80 caratteri.
                - "reason" spiega in una frase perché è o non è una richiesta di assistenza.
                - "confidence" è la tua confidenza nel verdetto, tra 0 e 1.
                - "language" è il codice lingua ISO 639-1 dell'email (es. "it", "en", "es", "fr").

                SCHEMA:
                {"isSupportRequest": true, "confidence": 0.0, "title": "", "summary": "", "reason": "", "language": "it"}
                """;
        }

        private static string BuildUserPrompt(string subject, string? body)
        {
            var text = body ?? string.Empty;
            if (text.Length > MaxBodyChars)
                text = text.Substring(0, MaxBodyChars);

            var sb = new StringBuilder();
            sb.AppendLine("Oggetto:");
            sb.AppendLine(string.IsNullOrWhiteSpace(subject) ? "(nessun oggetto)" : subject);
            sb.AppendLine();
            sb.AppendLine("Corpo:");
            sb.AppendLine(string.IsNullOrWhiteSpace(text) ? "(corpo vuoto)" : text);

            return sb.ToString();
        }

        /// <summary>Estrae il JSON dalla risposta (tollerante a eventuali fence markdown) e lo mappa.</summary>
        private InboundEmailTriage? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("Triage AI: risposta senza JSON riconoscibile");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
                var root = doc.RootElement;

                return new InboundEmailTriage(
                    IsSupportRequest: GetBool(root, "isSupportRequest"),
                    Confidence: GetDouble(root, "confidence"),
                    Title: GetString(root, "title"),
                    Summary: GetString(root, "summary"),
                    Reason: GetString(root, "reason"),
                    Language: GetString(root, "language"));
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Triage AI: JSON non valido");
                return null;
            }
        }

        private static bool GetBool(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        private static double GetDouble(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
                ? Math.Clamp(d, 0, 1)
                : 0;

        private static string? GetString(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
