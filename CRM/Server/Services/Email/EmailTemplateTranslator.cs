using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CRM.Shared.DTOs;

namespace CRM.Server.Services.Email
{
    public class EmailTemplateTranslator : IEmailTemplateTranslator
    {
        private readonly ILogger<EmailTemplateTranslator> _logger;
        private readonly AnthropicClient? _client;
        private readonly string _model;

        public EmailTemplateTranslator(IConfiguration configuration, ILogger<EmailTemplateTranslator> logger)
        {
            _logger = logger;

            var apiKey = configuration["Anthropic:ApiKey"];
            _model = configuration["Anthropic:ChatModel"] ?? "claude-opus-4-8";

            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_ANTHROPIC_API_KEY_HERE")
                _client = new AnthropicClient { ApiKey = apiKey };
        }

        public bool IsAvailable => _client != null;

        public async Task<IReadOnlyList<EmailTemplateVersionDTO>> TranslateAsync(
            string sourceLanguage, string subject, string? body,
            IReadOnlyList<string> targetLanguages, CancellationToken ct = default)
        {
            var targets = targetLanguages.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            if (_client == null || targets.Count == 0 || string.IsNullOrWhiteSpace(subject))
                return new List<EmailTemplateVersionDTO>();

            try
            {
                var response = await _client.Messages.Create(new MessageCreateParams
                {
                    Model = _model,
                    MaxTokens = 4000,
                    System = BuildSystemPrompt(),
                    OutputConfig = new OutputConfig { Effort = Effort.Medium },
                    Messages = new List<MessageParam>
                    {
                        new() { Role = Role.User, Content = BuildUserPrompt(sourceLanguage, subject, body, targets) }
                    },
                });

                var text = string.Concat(response.Content.Select(x => x.Value).OfType<TextBlock>().Select(x => x.Text));
                return Parse(text, targets);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Traduzione template email non riuscita");
                return new List<EmailTemplateVersionDTO>();
            }
        }

        private static string BuildSystemPrompt()
        {
            return """
                Sei un traduttore professionale di template email transazionali aziendali.
                Traduci OGGETTO e CORPO nelle lingue richieste.

                REGOLE INDEROGABILI:
                - NON tradurre né modificare i segnaposto: sequenze come $NAME, $URL, $COMPANY, $TICKET, $DATE
                  vanno lasciate identiche, nella stessa posizione logica.
                - Mantieni INTATTA la struttura e i tag HTML (<p>, <a>, <br>, attributi, ecc.): traduci solo il testo visibile.
                - Registro formale e cortese, coerente con una comunicazione aziendale.
                - Non aggiungere contenuti non presenti nell'originale.
                - Rispondi ESCLUSIVAMENTE con un array JSON valido, senza testo prima o dopo e senza blocchi markdown.

                SCHEMA (una voce per lingua richiesta):
                [{"language": "en", "subject": "...", "body": "..."}]
                """;
        }

        private static string BuildUserPrompt(string source, string subject, string? body, IReadOnlyList<string> targets)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Lingua sorgente: {source}");
            sb.AppendLine($"Lingue target (codici ISO): {string.Join(", ", targets)}");
            sb.AppendLine();
            sb.AppendLine("OGGETTO:");
            sb.AppendLine(subject);
            sb.AppendLine();
            sb.AppendLine("CORPO:");
            sb.AppendLine(string.IsNullOrEmpty(body) ? "(vuoto)" : body);
            return sb.ToString();
        }

        private IReadOnlyList<EmailTemplateVersionDTO> Parse(string? text, IReadOnlyList<string> targets)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<EmailTemplateVersionDTO>();

            var start = text.IndexOf('[');
            var end = text.LastIndexOf(']');
            if (start < 0 || end <= start)
            {
                _logger.LogWarning("Traduzione: risposta senza array JSON riconoscibile");
                return new List<EmailTemplateVersionDTO>();
            }

            try
            {
                using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
                var list = new List<EmailTemplateVersionDTO>();

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var lang = GetString(el, "language")?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(lang))
                        continue;

                    list.Add(new EmailTemplateVersionDTO
                    {
                        Language = lang!,
                        Subject = GetString(el, "subject"),
                        Body = GetString(el, "body")
                    });
                }

                // Tiene solo le lingue effettivamente richieste.
                return list.Where(v => targets.Contains(v.Language)).ToList();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Traduzione: JSON non valido");
                return new List<EmailTemplateVersionDTO>();
            }
        }

        private static string? GetString(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
