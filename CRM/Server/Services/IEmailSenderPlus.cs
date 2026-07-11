using CRM.Server.Helpers;
using CRM.Shared;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public interface IEmailSenderPlus: IEmailSender
    {
        Task<MimeMessage?> SendEmailAsync(List<string> to, EmailsTypes emailsType, List<string>? attachments, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null);

        Task<MimeMessage?> SendEmailAsync(string to, EmailsTypes emailsType, List<string>? attachments, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null);

        Task<bool> SendEmailAsync(List<string> to, EmailsTypes emailType, List<string>? attachments, string subject, string message, Dictionary<string, string>? keyValues, string? cc = null, EmailContext? context = null, string? culture = null);
    }
}
