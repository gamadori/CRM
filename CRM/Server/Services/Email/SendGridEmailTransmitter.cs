using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Trasmettitore basato sull'API di SendGrid (SDK ufficiale). Converte il messaggio MIME nei
    /// campi attesi (mittente/destinatari/oggetto/HTML/allegati). Come per Brevo, le immagini inline
    /// via cid non vengono renderizzate dall'API: per il logo dei template usare il relay SMTP di
    /// SendGrid (<c>smtp.sendgrid.net</c>) configurandolo come canale SMTP.
    /// </summary>
    public sealed class SendGridEmailTransmitter : IEmailTransmitter
    {
        private readonly SendGridClient _client;
        private readonly string _senderEmail;
        private readonly string? _senderName;

        public SendGridEmailTransmitter(string name, string apiKey, string senderEmail, string? senderName)
        {
            Name = name;
            _client = new SendGridClient(apiKey);
            _senderEmail = senderEmail;
            _senderName = senderName;
        }

        public string Name { get; }

        public async Task<string> SendAsync(MimeMessage message, string? messageRef = null, CancellationToken ct = default)
        {
            var mail = new SendGridMessage
            {
                From = new EmailAddress(_senderEmail, string.IsNullOrWhiteSpace(_senderName) ? null : _senderName),
                Subject = message.Subject ?? string.Empty,
                HtmlContent = string.IsNullOrEmpty(message.HtmlBody) ? (message.TextBody ?? " ") : message.HtmlBody
            };

            if (!string.IsNullOrEmpty(message.TextBody))
                mail.PlainTextContent = message.TextBody;

            // Correlazione: il custom arg viene restituito negli eventi webhook di SendGrid.
            if (!string.IsNullOrEmpty(messageRef))
                mail.CustomArgs = new Dictionary<string, string> { ["ref"] = messageRef };

            foreach (var to in message.To.Mailboxes)
                mail.AddTo(new EmailAddress(to.Address, to.Name));

            foreach (var cc in message.Cc.Mailboxes)
                mail.AddCc(new EmailAddress(cc.Address, cc.Name));

            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    part.Content.DecodeTo(ms);
                    mail.AddAttachment(part.FileName ?? "allegato", Convert.ToBase64String(ms.ToArray()));
                }
            }

            var response = await _client.SendEmailAsync(mail, ct);

            if ((int)response.StatusCode >= 400)
            {
                var body = await response.Body.ReadAsStringAsync(ct);
                throw new Exception($"SendGrid {(int)response.StatusCode}: {body}");
            }

            return $"SendGrid {(int)response.StatusCode}";
        }
    }
}
