using CRM.Server.Data;
using CRM.Server.Services.Email;
using CRM.Shared;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace CRM.Server.Services
{
    /// <summary>
    /// Motore di ricezione posta (Tier 4, Fase 1). Periodicamente legge in IMAP le caselle attive in
    /// modalità <see cref="EmailInboxMode.Imap"/>, scarica i messaggi non letti e li passa al router
    /// (<see cref="IInboundEmailRouter"/>) che li registra e crea le attività. I messaggi processati
    /// vengono marcati come letti (idempotenza col supporto della deduplica del router).
    /// Le caselle inbound-parse ESP NON sono lette qui: arrivano via webhook.
    /// </summary>
    public class EmailInboxBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailInboxBackgroundService> _logger;

        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

        public EmailInboxBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EmailInboxBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); } catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailInboxBackgroundService: errore nel ciclo");
                }

                try { await Task.Delay(Interval, stoppingToken); } catch (TaskCanceledException) { break; }
            }
        }

        private async Task PollAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var router = sp.GetRequiredService<IInboundEmailRouter>();

            var inboxes = await db.EmailInboxes
                .Where(i => i.IsActive && i.Mode == EmailInboxMode.Imap)
                .AsNoTracking()
                .ToListAsync(ct);

            foreach (var inbox in inboxes)
            {
                if (string.IsNullOrWhiteSpace(inbox.Host) || string.IsNullOrWhiteSpace(inbox.Username))
                    continue;

                try
                {
                    await PollInboxAsync(inbox, router, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "EmailInboxBackgroundService: errore sulla casella {Inbox}", inbox.DisplayName);
                }
            }
        }

        private async Task PollInboxAsync(EmailInbox inbox, IInboundEmailRouter router, CancellationToken ct)
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, chain, e) => true;

            var socket = inbox.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(inbox.Host, inbox.Port, socket, ct);
            await client.AuthenticateAsync(inbox.Username, inbox.Password, ct);

            var folder = string.IsNullOrWhiteSpace(inbox.Folder) || inbox.Folder.Equals("INBOX", StringComparison.OrdinalIgnoreCase)
                ? client.Inbox
                : await client.GetFolderAsync(inbox.Folder, ct);

            await folder.OpenAsync(FolderAccess.ReadWrite, ct);

            var uids = await folder.SearchAsync(SearchQuery.NotSeen, ct);

            foreach (var uid in uids)
            {
                var message = await folder.GetMessageAsync(uid, ct);
                var sender = message.From.Mailboxes.FirstOrDefault();

                var inboundMessage = new InboundMessage
                {
                    InboxId = inbox.Id,
                    MessageId = string.IsNullOrEmpty(message.MessageId) ? null : message.MessageId,
                    Uid = uid.Id,
                    FromAddress = sender?.Address,
                    FromName = sender?.Name,
                    ToAddress = inbox.Address,
                    Subject = message.Subject,
                    Body = message.TextBody ?? HtmlToText(message.HtmlBody),
                    ReceivedAt = message.Date.LocalDateTime,
                    Attachments = ExtractAttachments(message)
                };

                await router.IngestAsync(inboundMessage, ct);

                // Marca come letto: già processato (la deduplica evita duplicati anche se ricapita).
                await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
            }

            await client.DisconnectAsync(true, ct);
        }

        private static List<InboundAttachment> ExtractAttachments(MimeMessage message)
        {
            var list = new List<InboundAttachment>();

            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                {
                    using var ms = new MemoryStream();
                    part.Content.DecodeTo(ms);
                    list.Add(new InboundAttachment
                    {
                        FileName = part.FileName ?? "allegato",
                        ContentType = part.ContentType?.MimeType,
                        Content = ms.ToArray()
                    });
                }
            }

            return list;
        }

        private static string? HtmlToText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(text);
        }
    }
}
