using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using CRM.Shared.Extensions;
using CRM.Shared.Resources.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using MimeKit;
using MimeKit.Text;
using MimeKit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Duende.IdentityServer.Models.IdentityResources;

namespace CRM.Server.Services
{
    public class EmailService: IEmailSenderPlus
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

        public async Task<MimeMessage?> SendEmailAsync(string to, EmailsTypes emailType, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null)
        {
            try
            {
                List<string> list = new List<string>();
                list.Add(to);

                return await SendEmailAsync(list, emailType, attachments, keyValues, cc);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        public async Task<MimeMessage?> SendEmailAsync(List<string> to, EmailsTypes emailType, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null)
        {
            try
            {
                var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

                if (settings != null)
                {
                    MimeMessage? email = await _emailBuilderService.CreateEmail(emailType, settings.SenderName, settings.SenderEmail, to, attachments, keyValues, cc);

                    // Invio della email
                    using (var smtp = new SmtpClient())
                    {
                        System.Net.ServicePointManager.ServerCertificateValidationCallback =
                        (sender, certificate, chain, sslPolicyErrors) => true;

                        await smtp.ConnectAsync(settings.Server, settings.Port, settings.Ssl); //SecureSocketOptions.StartTls);
                        await smtp.AuthenticateAsync(settings.Username, settings.Password);

                        var result = await smtp.SendAsync(email);
                        await smtp.DisconnectAsync(true);

                        await RegEmailSent(to.FromList(), cc, attachments?.FromList(), email?.Subject ?? "", email?.HtmlBody ?? "", result);
                    }

                    return email;

                }
                else
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Info, "Non è possibile inviare le email smtp non settato");
                    return null;
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

       


        public async Task<bool> SendEmailAsync(List<string> to, EmailsTypes emailType, List<string> attachments, string subject, string message, Dictionary<string, string>? keyValues, string? cc = null)
        {
            try
            {
                var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

                if (settings != null)
                {
                    MimeMessage? email = await _emailBuilderService.CreateEmail(emailType, settings.SenderName, settings.SenderEmail, to, subject, message, attachments, keyValues, cc);

                    // Invio della email
                    if (email != null)
                    {
                        using (var smtp = new SmtpClient())
                        {
                            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                            (sender, certificate, chain, sslPolicyErrors) => true;

                            await smtp.ConnectAsync(settings.Server, settings.Port, settings.Ssl); //SecureSocketOptions.StartTls);
                            await smtp.AuthenticateAsync(settings.Username, settings.Password);

                            var resp = await smtp.SendAsync(email);
                            await smtp.DisconnectAsync(true);

                            await RegEmailSent(to.FromList(), cc, attachments?.FromList(), subject, message, resp);
                        }
                    }
                    else
                    {
                        await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, "Email Template not Found!");
                        return false;
                    }
                    return true;
                }
                else
                {
                    await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, "Smtp not settings");
                    return false;
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }



        public async Task SendEmailAsync(string to, string subject, string message)
        {
            try
            {
                

                //return Execute(Options.SendGridKey, subject, message, email);
                var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

                if (settings != null)
                {
                    // Creazione del messaggio
                
                    MimeMessage email = new MimeMessage();
                    email.Sender = MailboxAddress.Parse(settings.SenderEmail);
                    email.To.Add(MailboxAddress.Parse(to));
                    email.Subject = subject;
                    email.Body = new TextPart(TextFormat.Html) { Text = message };
                    email.From.Add(MailboxAddress.Parse(settings.SenderEmail));
                    // Invio della email
                    using (var smtp = new SmtpClient())
                    {
                        System.Net.ServicePointManager.ServerCertificateValidationCallback =
                        (sender, certificate, chain, sslPolicyErrors) => true;

                        await smtp.ConnectAsync(settings.Server, settings.Port, settings.Ssl); //SecureSocketOptions.StartTls);
                        await smtp.AuthenticateAsync(settings.Username, settings.Password);
                        
                        var result = await smtp.SendAsync(email);
                        await smtp.DisconnectAsync(true);

                        await RegEmailSent(to, "", "", subject, message, result);
                    }
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(SendEmailAsync), LogEvent.EventsTypes.Error, ex);
            }
        }

        private async Task RegEmailSent(string to, string? cc, string? attachments, string subject, string message, string result)
        {
            try
            {
                EmailSent emailSent = new EmailSent();
                emailSent.DateSent = DateTime.Now;
                emailSent.Subject = subject;
                emailSent.IdUser = await _permits.IdUser();
                emailSent.Message = message;
                emailSent.Result = result;
                emailSent.To = to;
                emailSent.CC = cc ?? "";
                emailSent.Attchments = attachments;
               
                _context.EmailsSent.Add(emailSent);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailService), nameof(RegEmailSent), LogEvent.EventsTypes.Error, ex);
            }
        }
    }
}
