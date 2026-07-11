using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.Extensions;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio di invio email. NON trasmette in linea: renderizza il messaggio (template, logo,
    /// allegati) e lo <b>accoda</b> nell'outbox (<see cref="EmailOutbox"/>) restituendo subito il
    /// controllo. La trasmissione SMTP effettiva, con retry e backoff, è a carico di
    /// <see cref="EmailOutboxBackgroundService"/>. In questo modo la richiesta non si blocca sull'SMTP,
    /// l'invio è durevole (sopravvive a riavvii e a server mail irraggiungibili) e transazionale con
    /// l'operazione di business che lo genera.
    /// </summary>
    public class EmailService : IEmailSenderPlus
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailBuilderService _emailBuilderService;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public EmailService(ApplicationDbContext context, IEmailBuilderService emailBuilderService, ILogEventService logEventService, IPermitsService permitsService)
        {
            _context = context;
            _emailBuilderService = emailBuilderService;
            _logEventService = logEventService;
            _permits = permitsService;
        }

        public async Task<MimeMessage?> SendEmailAsync(string to, EmailsTypes emailType, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null)
        {
            return await SendEmailAsync(new List<string> { to }, emailType, attachments, keyValues, cc, context, culture);
        }

        public async Task<MimeMessage?> SendEmailAsync(List<string> to, EmailsTypes emailType, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null)
        {
            try
            {
                var settings = await GetPrimaryChannelAsync();
                if (settings == null)
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Info, "Non è possibile inviare le email: SMTP non settato");
                    return null;
                }

                MimeMessage? email = await _emailBuilderService.CreateEmail(emailType, settings.SenderName, settings.SenderEmail, to, attachments, keyValues, cc, culture);
                if (email == null)
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, "Email Template not Found!");
                    return null;
                }

                await EnqueueAsync(email, emailType, to.FromList(), cc, attachments?.FromList(), context);
                return email;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<bool> SendEmailAsync(List<string> to, EmailsTypes emailType, List<string> attachments, string subject, string message, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null)
        {
            try
            {
                var settings = await GetPrimaryChannelAsync();
                if (settings == null)
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, "Smtp not settings");
                    return false;
                }

                MimeMessage? email = await _emailBuilderService.CreateEmail(emailType, settings.SenderName, settings.SenderEmail, to, subject, message, attachments, keyValues, cc, culture);
                if (email == null)
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, "Email Template not Found!");
                    return false;
                }

                await EnqueueAsync(email, emailType, to.FromList(), cc, attachments?.FromList(), context);
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }

        /// <summary>
        /// Overload di <see cref="IEmailSender"/> (senza template): usata dai flussi Identity
        /// (conferma email, recupero password). Anche questa viene accodata nell'outbox.
        /// </summary>
        public async Task SendEmailAsync(string to, string subject, string message)
        {
            try
            {
                var settings = await GetPrimaryChannelAsync();
                if (settings == null)
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Info, "Non è possibile inviare le email: SMTP non settato");
                    return;
                }

                var email = new MimeMessage();
                email.Sender = MailboxAddress.Parse(settings.SenderEmail);
                email.From.Add(MailboxAddress.Parse(settings.SenderEmail));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new TextPart(TextFormat.Html) { Text = message };

                await EnqueueAsync(email, EmailsTypes.ConfirmEmail, to, null, null, null);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
            }
        }

        /// <summary>
        /// Accoda il messaggio già renderizzato: serializza il MIME (lossless) e persiste la riga
        /// di outbox in stato Pending. La trasmissione avverrà nel background service.
        /// </summary>
        private async Task EnqueueAsync(MimeMessage message, EmailsTypes emailType, string to, string? cc, string? attachments, EmailContext? context)
        {
            var outbox = new EmailOutbox
            {
                EmailType = emailType,
                To = to,
                Cc = cc ?? string.Empty,
                Subject = message.Subject ?? string.Empty,
                Attachments = attachments,
                Payload = await SerializeAsync(message),
                IdUser = await _permits.IdUser(),
                EntityType = context?.EntityType,
                EntityId = context?.EntityId,
                Status = EmailOutboxStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.EmailsOutbox.Add(outbox);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Canale a priorità più alta (tra quelli attivi): fornisce l'identità del mittente
        /// (SenderName/SenderEmail) con cui viene renderizzato il messaggio. La trasmissione
        /// effettiva percorre poi l'intera catena di canali nel worker dell'outbox.
        /// </summary>
        private Task<SmtpSetting?> GetPrimaryChannelAsync() =>
            _context.SmtpSettings
                .Where(s => s.IsActive)
                .OrderBy(s => s.Priority)
                .ThenBy(s => s.Id)
                .FirstOrDefaultAsync();

        private static async Task<byte[]> SerializeAsync(MimeMessage message)
        {
            using var stream = new MemoryStream();
            await message.WriteToAsync(stream);
            return stream.ToArray();
        }
    }
}
