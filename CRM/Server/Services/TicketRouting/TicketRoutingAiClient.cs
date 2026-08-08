using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CRM.Server.Services.Usage;
using CRM.Shared;

namespace CRM.Server.Services.TicketRouting
{
    /// <summary>
    /// Sceglie il gruppo con Claude. Come gli altri servizi AI dell'applicazione tiene un client
    /// <b>nullable</b>: senza chiave API resta inerte invece di far fallire l'apertura di un ticket.
    /// </summary>
    public class TicketRoutingAiClient : ITicketRoutingAiClient
    {
        /// <summary>Oltre questa lunghezza la descrizione non aggiunge informazione utile alla scelta.</summary>
        private const int MaxDescriptionChars = 4000;

        private readonly ILogger<TicketRoutingAiClient> _logger;
        private readonly IUsageRecorder _usage;
        private readonly AnthropicClient? _client;

        public TicketRoutingAiClient(IConfiguration configuration, ILogger<TicketRoutingAiClient> logger, IUsageRecorder usage)
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

        public async Task<TicketRoutingSuggestion?> SuggestGroupAsync(TicketRoutingRequest request, string? modelOverride = null, CancellationToken ct = default)
        {
            if (_client == null || request.Candidates.Count == 0)
                return null;

            // Il modello effettivo, non quello di default: e' quello che finisce a listino.
            var model = string.IsNullOrWhiteSpace(modelOverride) ? Model : modelOverride!;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = model,
                    MaxTokens = 400,
                    System = BuildSystemPrompt(),
                    OutputConfig = new OutputConfig { Effort = Effort.Medium },
                    Messages = new List<MessageParam>
                    {
                        new() { Role = Role.User, Content = BuildUserPrompt(request) }
                    },
                });

                await _usage.RecordTokensAsync(
                    ExternalServiceFeature.TicketRouting, model, response.TokenUsageOf(),
                    true, stopwatch.ElapsedMilliseconds);

                var text = string.Concat(
                    response.Content
                        .Select(x => x.Value)
                        .OfType<TextBlock>()
                        .Select(x => x.Text));

                return Parse(text, request);
            }
            catch (Exception ex)
            {
                // Non bloccante: il ticket viene comunque creato e resta nella coda generale.
                _logger.LogWarning(ex, "Smistamento AI del ticket non riuscito");
                return null;
            }
        }

        private static string BuildSystemPrompt()
        {
            return """
                Sei lo smistatore dei ticket di un'assistenza tecnica: leggi la richiesta e scegli il
                gruppo di lavoro che deve prenderla in carico.

                REGOLE:
                - Scegli SOLO tra i gruppi elencati, restituendo il loro id numerico. Non inventare id.
                - Se nessun gruppo e' chiaramente competente, o la richiesta e' troppo vaga per decidere,
                  restituisci "groupId": null: e' meglio lasciare il ticket in coda che smistarlo male.
                - Basati sulle competenze dichiarate di ogni gruppo, sul tipo di ticket e sul contenuto
                  della richiesta. Non dedurre nulla dal nome dell'azienda cliente.
                - "confidence" e' la tua confidenza reale nella scelta, tra 0 e 1. Usa valori alti solo
                  quando la corrispondenza con le competenze del gruppo e' esplicita; se stai scegliendo
                  per esclusione o per analogia, resta sotto 0.7.
                - "reason" spiega la scelta in una frase, in italiano, citando l'elemento decisivo.
                - Rispondi ESCLUSIVAMENTE con un oggetto JSON valido, senza testo prima o dopo,
                  senza blocchi markdown.

                SCHEMA:
                {"groupId": 3, "confidence": 0.0, "reason": ""}
                """;
        }

        private static string BuildUserPrompt(TicketRoutingRequest request)
        {
            var description = request.Description ?? string.Empty;
            if (description.Length > MaxDescriptionChars)
                description = description.Substring(0, MaxDescriptionChars);

            var sb = new StringBuilder();

            sb.AppendLine("GRUPPI DISPONIBILI:");
            foreach (var candidate in request.Candidates)
            {
                sb.AppendLine($"- id {candidate.Id} | {candidate.Name}");

                if (!string.IsNullOrWhiteSpace(candidate.Hints))
                    sb.AppendLine($"  competenze: {candidate.Hints!.Trim()}");
                else if (!string.IsNullOrWhiteSpace(candidate.Description))
                    sb.AppendLine($"  descrizione: {candidate.Description!.Trim()}");
                else
                    sb.AppendLine("  competenze: non dichiarate");
            }

            sb.AppendLine();
            sb.AppendLine("TICKET DA SMISTARE:");
            sb.AppendLine($"Tipo: {(string.IsNullOrWhiteSpace(request.TicketType) ? "non specificato" : request.TicketType)}");

            if (!string.IsNullOrWhiteSpace(request.Product))
                sb.AppendLine($"Prodotto: {request.Product}");

            if (!string.IsNullOrWhiteSpace(request.Company))
                sb.AppendLine($"Azienda: {request.Company}");

            sb.AppendLine();
            sb.AppendLine("Richiesta:");
            sb.AppendLine(string.IsNullOrWhiteSpace(description) ? "(nessuna descrizione)" : description);

            return sb.ToString();
        }

        /// <summary>
        /// Estrae il JSON dalla risposta (tollerante a eventuali fence markdown) e scarta i gruppi
        /// che non erano tra i candidati: un id inventato deve valere quanto una mancata risposta.
        /// </summary>
        private TicketRoutingSuggestion? Parse(string? text, TicketRoutingRequest request)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("Smistamento AI: risposta senza JSON riconoscibile");
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
                var root = doc.RootElement;

                var groupId = GetInt(root, "groupId");
                var confidence = GetDouble(root, "confidence");
                var reason = GetString(root, "reason");

                if (groupId != null && !request.Candidates.Any(c => c.Id == groupId.Value))
                {
                    _logger.LogWarning("Smistamento AI: gruppo {GroupId} non tra i candidati, suggerimento scartato", groupId);
                    return new TicketRoutingSuggestion(null, 0, reason);
                }

                return new TicketRoutingSuggestion(groupId, confidence, reason);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Smistamento AI: JSON non valido");
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
