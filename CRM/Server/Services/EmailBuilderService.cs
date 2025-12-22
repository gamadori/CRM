using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public class EmailBuilderService : IEmailBuilderService
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;
        public EmailBuilderService(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService; 
        }



        public async Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null)
        {

            string subject;
            string html;
            Logo logo = null;
            MimeMessage msg;
           
            EmailTemplate? emailTemplate = await _context.EmailTemplates.Include(x => x.Logo).Where(x => x.Tipo == typeEmail).FirstOrDefaultAsync();

            if (emailTemplate != null)
            {
                subject = emailTemplate.Subject;
                html = emailTemplate.Body;

                if (emailTemplate.IdLogo != null)
                {
                    logo = emailTemplate.Logo;
                }
                return CreateEmail(fromName, from, to, subject, attachments, html, keyValues, logo, cc);
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(EmailBuilderService), nameof(CreateEmail), LogEvent.EventsTypes.Warning, "Email Template non settato");
                return null;
            }
        }



        public async Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, string subject, string message, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null)
        {


            string html;
            Logo? logo = null;

            EmailTemplate? emailTemplate = await _context.EmailTemplates.Include(x => x.Logo).Where(x => x.Tipo == typeEmail).FirstOrDefaultAsync();

            if (emailTemplate != null)
            {
                subject = emailTemplate.Subject + subject;
                html = emailTemplate.Body + message;

                if (emailTemplate.IdLogo != null)
                {
                    logo = emailTemplate.Logo;
                }
                return CreateEmail(fromName, from, to, subject, attachments, html, keyValues, logo, cc);
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(EmailBuilderService), nameof(CreateEmail), LogEvent.EventsTypes.Warning, "Email Template non settato");
                return null;
            }
        }

        public MimeMessage CreateEmail(string fromName, string from, List<string> to, string subject, List<string> attachments, string html, Dictionary<string, string>? keyValues, Logo? logo, string? cc = null)
        {
            var message = new MimeMessage();
            message.Sender = MailboxAddress.Parse(from);

            foreach (var a in to)
            {
                message.To.Add(MailboxAddress.Parse(a));
            }
            message.Subject = subject;
            message.Body = CreateBody(html, keyValues, logo, attachments);
            message.From.Add(new MailboxAddress(fromName, from));

            if (cc != null && cc.Length > 0)
            {
                string[] CCId = cc.Split(',');
                foreach (string CCEmail in CCId)
                {
                    message.Cc.Add(new MailboxAddress(CCEmail, CCEmail));
                    
                }

                
            }
            return message;
        }
        private MimeEntity CreateBody(string html, Dictionary<string, string>? keyValues, Logo? logo, List<string> attachments)
        {
            BodyBuilder builder = new BodyBuilder();

            if (keyValues != null)
            {
                foreach (var item in keyValues)
                {
                    html = html.Replace(item.Key, item.Value);
                }
            }

            builder.TextBody = Regex.Replace(html, "<.*?>", String.Empty); ;

            if (logo != null)
            {

                html += "<p><img src=\"cid:{0}\"></p>";


                var image = builder.LinkedResources.Add(logo.Codice, Convert.FromBase64String(FileStream(logo.InputFile)));
                image.ContentId = MimeUtils.GenerateMessageId();

                builder.HtmlBody = string.Format(html, image.ContentId);
            }
            else
                builder.HtmlBody = html;

            if (attachments != null)
            {
                foreach (var a in attachments)
                    builder.Attachments.Add(a);
            }

            return builder.ToMessageBody();
        }




        private string FileStream(string logo)
        {
            var p = logo.IndexOf(',');
            if (p >= 0 && p < logo.Length - 1)
            {
                return logo.Substring(p + 1);
            }
            else
                return logo;
        }

    }
       
}
