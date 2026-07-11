using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services.Email;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class EmailBuilderService : IEmailBuilderService
    {
        /// <summary>Lingua usata come fallback quando manca il template nella lingua richiesta.</summary>
        private const string DefaultCulture = "it";

        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;

        public EmailBuilderService(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        public async Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, string? culture = null)
        {
            var template = await GetTemplateAsync(typeEmail, culture);
            if (template == null)
            {
                await _logEventService.RegisterAsync(nameof(EmailBuilderService), nameof(CreateEmail), LogEvent.EventsTypes.Warning, $"Nessun template configurato per {typeEmail}");
                return null;
            }

            return CreateEmail(fromName, from, to, template.Subject, attachments, template.Body, keyValues, template.Logo, cc);
        }

        public async Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, string subject, string message, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, string? culture = null)
        {
            var template = await GetTemplateAsync(typeEmail, culture);

            // Con template: oggetto/corpo del template + testo specifico della chiamata. Senza: solo il testo passato.
            if (template != null)
                return CreateEmail(fromName, from, to, template.Subject + subject, attachments, template.Body + message, keyValues, template.Logo, cc);

            return CreateEmail(fromName, from, to, subject, attachments, message, keyValues, null, cc);
        }

        public MimeMessage CreateEmail(string fromName, string from, List<string> to, string subject, List<string> attachments, string html, Dictionary<string, string>? keyValues, Logo? logo, string? cc = null)
        {
            var message = new MimeMessage();
            message.Sender = MailboxAddress.Parse(from);
            message.From.Add(new MailboxAddress(fromName, from));

            foreach (var a in to)
                message.To.Add(MailboxAddress.Parse(a));

            if (!string.IsNullOrEmpty(cc))
            {
                foreach (var ccEmail in cc.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    message.Cc.Add(new MailboxAddress(ccEmail, ccEmail));
            }

            message.Subject = EmailTemplateRenderer.Render(subject, keyValues);
            message.Body = CreateBody(html, keyValues, logo, attachments);

            return message;
        }

        private MimeEntity CreateBody(string html, Dictionary<string, string>? keyValues, Logo? logo, List<string> attachments)
        {
            var builder = new BodyBuilder();

            html = EmailTemplateRenderer.Render(html, keyValues);

            builder.TextBody = Regex.Replace(html, "<.*?>", string.Empty);

            if (logo != null)
            {
                var image = builder.LinkedResources.Add(logo.Codice, Convert.FromBase64String(FileStream(logo.InputFile)));
                image.ContentId = MimeUtils.GenerateMessageId();

                // Il cid è calcolato prima e concatenato: nessun string.Format sull'HTML dell'utente
                // (le graffe di eventuale CSS non provocano più FormatException).
                builder.HtmlBody = html + $"<p><img src=\"cid:{image.ContentId}\"></p>";
            }
            else
            {
                builder.HtmlBody = html;
            }

            if (attachments != null)
            {
                foreach (var a in attachments)
                    builder.Attachments.Add(a);
            }

            return builder.ToMessageBody();
        }

        /// <summary>
        /// Risolve il template per tipo e lingua con fallback: lingua richiesta → lingua di default →
        /// template senza lingua → qualunque template del tipo. Ritorna null solo se il tipo non ha template.
        /// </summary>
        private async Task<EmailTemplate?> GetTemplateAsync(EmailsTypes tipo, string? culture)
        {
            var wanted = Normalize(culture);

            var candidates = await _context.EmailTemplates
                .Include(x => x.Logo)
                .Where(x => x.Tipo == tipo)
                .ToListAsync();

            if (candidates.Count == 0)
                return null;

            return candidates.FirstOrDefault(x => Normalize(x.Language) == wanted)
                ?? candidates.FirstOrDefault(x => Normalize(x.Language) == DefaultCulture)
                ?? candidates.FirstOrDefault(x => string.IsNullOrEmpty(x.Language))
                ?? candidates.First();
        }

        /// <summary>Riduce una cultura ("it-IT") al codice lingua ("it"); null/vuoto → lingua di default.</summary>
        private static string Normalize(string? culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
                return DefaultCulture;

            var dash = culture.IndexOf('-');
            return (dash > 0 ? culture.Substring(0, dash) : culture).Trim().ToLowerInvariant();
        }

        private static string FileStream(string logo)
        {
            var p = logo.IndexOf(',');
            return p >= 0 && p < logo.Length - 1 ? logo.Substring(p + 1) : logo;
        }
    }
}
