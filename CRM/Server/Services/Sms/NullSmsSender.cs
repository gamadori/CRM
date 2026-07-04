using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CRM.Server.Services.Sms
{
    /// <summary>
    /// Implementazione usata quando nessun provider SMS è configurato: non invia
    /// nulla e restituisce false, così il chiamante può ripiegare su un altro
    /// canale (es. email).
    /// </summary>
    public class NullSmsSender : ISmsSender
    {
        private readonly ILogger<NullSmsSender> _logger;

        public NullSmsSender(ILogger<NullSmsSender> logger) => _logger = logger;

        public bool IsConfigured => false;

        public Task<bool> SendAsync(string toPhoneE164, string text, CancellationToken ct = default)
        {
            _logger.LogWarning("SMS non inviato: nessun provider SMS configurato (destinatario {Phone}).", toPhoneE164);
            return Task.FromResult(false);
        }
    }
}
