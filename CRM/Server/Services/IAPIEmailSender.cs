using CRM.Server.Models;

namespace CRM.Server.Services
{
    public interface IAPIEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken ct = default);
    }
}
