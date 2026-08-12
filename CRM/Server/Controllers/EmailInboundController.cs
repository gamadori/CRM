using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CRM.Server.Data;
using CRM.Server.Services.Email;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Riceve le email in ingresso via inbound-parse dei provider ESP (alternativa all'IMAP).
    /// Pubblico ma autenticato dal <c>token</c> in query, che identifica la casella
    /// (<see cref="EmailInbox.WebhookToken"/>). Il messaggio viene passato allo stesso
    /// <see cref="IInboundEmailRouter"/> usato dal polling IMAP.
    /// Gestisce sia il JSON (stile Brevo) sia il multipart/form-data (SendGrid Inbound Parse).
    /// </summary>
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailInboundController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IInboundEmailRouter _router;

        public EmailInboundController(ApplicationDbContext context, IInboundEmailRouter router)
        {
            _context = context;
            _router = router;
        }

        [HttpPost("{provider}")]
        public async Task<IActionResult> Receive(string provider, [FromQuery] string? token, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(token))
                return Unauthorized();

            // Il token e' cifrato sulla colonna, quindi il confronto NON puo' stare nella query:
            // ogni cifratura produce un testo diverso, e "WebhookToken == token" non troverebbe
            // mai niente. Si caricano le caselle inbound (sono una manciata) e si confronta in
            // memoria sul valore decifrato, a tempo costante perche' il token e' un segreto.
            var candidate = await _context.EmailInboxes.AsNoTracking()
                .Where(i => i.IsActive && i.Mode == EmailInboxMode.InboundParseEsp)
                .ToListAsync(ct);

            var inbox = candidate.FirstOrDefault(i => TokenMatches(i.WebhookToken, token));

            if (inbox == null)
                return Unauthorized();

            InboundMessage? message = Request.HasFormContentType
                ? ParseForm(Request.Form, inbox)          // SendGrid Inbound Parse (multipart/form-data)
                : await ParseJsonAsync(Request.Body, inbox, ct); // Brevo (JSON)

            if (message == null)
                return Ok(new { received = 0 });

            var ingested = await _router.IngestAsync(message, ct);
            return Ok(new { received = ingested ? 1 : 0 });
        }

        /// <summary>
        /// Confronto del token del webhook a tempo costante: e' un segreto, e un confronto che si
        /// ferma al primo carattere diverso racconta quanti ne ha indovinati chi prova.
        /// </summary>
        private static bool TokenMatches(string? salvato, string presentato)
        {
            if (string.IsNullOrEmpty(salvato))
                return false;

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(salvato),
                System.Text.Encoding.UTF8.GetBytes(presentato));
        }

        private static InboundMessage ParseForm(IFormCollection form, EmailInbox inbox)
        {
            var attachments = new List<InboundAttachment>();
            foreach (var file in form.Files)
            {
                using var ms = new System.IO.MemoryStream();
                file.CopyTo(ms);
                attachments.Add(new InboundAttachment
                {
                    FileName = string.IsNullOrWhiteSpace(file.FileName) ? "allegato" : file.FileName,
                    ContentType = file.ContentType,
                    Content = ms.ToArray()
                });
            }

            return new InboundMessage
            {
                InboxId = inbox.Id,
                FromAddress = ExtractAddress(form["from"]),
                Subject = form["subject"],
                Body = form["text"].Count > 0 ? form["text"].ToString() : form["html"].ToString(),
                ToAddress = inbox.Address,
                ReceivedAt = System.DateTime.Now,
                Attachments = attachments
            };
        }

        private static async Task<InboundMessage?> ParseJsonAsync(System.IO.Stream body, EmailInbox inbox, CancellationToken ct)
        {
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            var e = doc.RootElement;

            if (e.ValueKind == JsonValueKind.Array)
                e = e.EnumerateArray().FirstOrDefault();

            if (e.ValueKind != JsonValueKind.Object)
                return null;

            return new InboundMessage
            {
                InboxId = inbox.Id,
                MessageId = GetString(e, "message-id") ?? GetString(e, "MessageId"),
                FromAddress = ExtractAddress(GetString(e, "from") ?? GetString(e, "sender") ?? GetString(e, "From")),
                Subject = GetString(e, "subject") ?? GetString(e, "Subject"),
                Body = GetString(e, "text") ?? GetString(e, "TextBody") ?? GetString(e, "html") ?? GetString(e, "RawHtmlBody"),
                ToAddress = inbox.Address,
                ReceivedAt = System.DateTime.Now
            };
        }

        private static string? ExtractAddress(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var start = raw.IndexOf('<');
            var end = raw.IndexOf('>');
            if (start >= 0 && end > start)
                return raw.Substring(start + 1, end - start - 1).Trim();
            return raw.Trim();
        }

        private static string? GetString(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
