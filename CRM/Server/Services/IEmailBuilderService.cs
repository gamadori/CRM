using CRM.Shared;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public interface IEmailBuilderService
    {
        Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, string? culture = null);

        Task<MimeMessage?> CreateEmail(EmailsTypes typeEmail, string fromName, string from, List<string> to, string subject, string message, List<string> attachments, Dictionary<string, string>? keyValues, string? cc = null, string? culture = null);

        MimeMessage CreateEmail(string fromName, string from, List<string> to, string subject, List<string> attachments, string html, Dictionary<string, string>? keyValues, Logo? logo, string? cc = null);
    }
}
