using CRM.Server.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Net.Mail;

namespace CRM.Server.Services
{
    public class SendGridEmailSender : IAPIEmailSender
    {
        private readonly SendGridClient _client;
        private readonly string _from;

        public SendGridEmailSender(string apiKey, string from)
        {
            _client = new SendGridClient(apiKey);
            _from = from;
        }

        public async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
        {
            var message = new SendGridMessage
            {
                From = new EmailAddress(_from),
                Subject = msg.Subject,
                HtmlContent = msg.HtmlBody,
                PlainTextContent = msg.TextBody
            };

            message.AddTo(new EmailAddress(msg.To));

            var response = await _client.SendEmailAsync(message, ct);

            if ((int)response.StatusCode >= 400)
            {
                throw new Exception($"SendGrid failed: {response.StatusCode}");
            }
        }
    }
}
