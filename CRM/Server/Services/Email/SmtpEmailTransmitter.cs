using MailKit.Net.Smtp;
using MimeKit;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Trasmettitore SMTP (MailKit). Ogni invio apre e chiude una propria connessione: è
    /// stateless e indipendente dagli altri canali, così il failover è pulito. Usato sia per il
    /// canale primario (credenziali da <c>SmtpSettings</c> in DB) sia per i fallback (appsettings).
    /// </summary>
    public sealed class SmtpEmailTransmitter : IEmailTransmitter
    {
        private readonly string _host;
        private readonly int _port;
        private readonly bool _ssl;
        private readonly string? _username;
        private readonly string? _password;

        public SmtpEmailTransmitter(string name, string host, int port, bool ssl, string? username, string? password)
        {
            Name = name;
            _host = host;
            _port = port;
            _ssl = ssl;
            _username = username;
            _password = password;
        }

        public string Name { get; }

        public async Task<string> SendAsync(MimeMessage message, string? messageRef = null, CancellationToken ct = default)
        {
            // SMTP puro non offre tracking di engagement: messageRef ignorato.
            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(_host, _port, _ssl, ct);

            // Alcuni relay interni non richiedono autenticazione: autentichiamo solo se ci sono credenziali.
            if (!string.IsNullOrEmpty(_username))
                await smtp.AuthenticateAsync(_username, _password, ct);

            var result = await smtp.SendAsync(message, ct);

            await smtp.DisconnectAsync(true, ct);

            return result;
        }
    }
}
