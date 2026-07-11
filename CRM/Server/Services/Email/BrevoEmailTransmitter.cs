using System.Net.Http.Json;
using MimeKit;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Trasmettitore basato sull'API transazionale di Brevo (POST /v3/smtp/email). Converte il
    /// messaggio MIME nei campi attesi dall'API (mittente/destinatari/oggetto/HTML/allegati).
    /// Nota: le immagini inline referenziate via cid (es. logo del template) non vengono
    /// renderizzate via API — per il pieno rendering usare il relay SMTP di Brevo come canale SMTP.
    /// </summary>
    public sealed class BrevoEmailTransmitter : IEmailTransmitter
    {
        private const string Endpoint = "https://api.brevo.com/v3/smtp/email";

        private readonly IHttpClientFactory _httpFactory;
        private readonly string _apiKey;
        private readonly string _senderEmail;
        private readonly string? _senderName;

        public BrevoEmailTransmitter(string name, IHttpClientFactory httpFactory, string apiKey, string senderEmail, string? senderName)
        {
            Name = name;
            _httpFactory = httpFactory;
            _apiKey = apiKey;
            _senderEmail = senderEmail;
            _senderName = senderName;
        }

        public string Name { get; }

        public async Task<string> SendAsync(MimeMessage message, string? messageRef = null, CancellationToken ct = default)
        {
            var payload = new Dictionary<string, object?>
            {
                ["sender"] = new { email = _senderEmail, name = string.IsNullOrWhiteSpace(_senderName) ? _senderEmail : _senderName },
                ["to"] = message.To.Mailboxes.Select(m => new { email = m.Address, name = m.Name }).ToList(),
                ["subject"] = message.Subject ?? string.Empty,
                ["htmlContent"] = string.IsNullOrEmpty(message.HtmlBody) ? (message.TextBody ?? " ") : message.HtmlBody
            };

            if (!string.IsNullOrEmpty(message.TextBody))
                payload["textContent"] = message.TextBody;

            // Correlazione: il tag viene restituito negli eventi webhook di Brevo.
            if (!string.IsNullOrEmpty(messageRef))
                payload["tags"] = new[] { messageRef };

            var cc = message.Cc.Mailboxes.Select(m => new { email = m.Address, name = m.Name }).ToList();
            if (cc.Count > 0)
                payload["cc"] = cc;

            var attachments = ExtractAttachments(message);
            if (attachments.Count > 0)
                payload["attachment"] = attachments;

            using var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Headers.Add("api-key", _apiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Brevo {(int)response.StatusCode}: {body}");

            return $"Brevo {(int)response.StatusCode}: {body}";
        }

        private static List<object> ExtractAttachments(MimeMessage message)
        {
            var list = new List<object>();

            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    part.Content.DecodeTo(ms);
                    list.Add(new
                    {
                        name = part.FileName ?? "allegato",
                        content = Convert.ToBase64String(ms.ToArray())
                    });
                }
            }

            return list;
        }
    }
}
