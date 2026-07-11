using System.Text.Json;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Ingerisce gli eventi di engagement inviati dai provider ESP via webhook, li normalizza e
    /// aggiorna l'email inviata (stato + contatori) e lo storico eventi.
    /// </summary>
    public interface IEmailEngagementService
    {
        /// <summary>Ingerisce il payload del webhook eventi di SendGrid (array JSON di eventi).</summary>
        Task<int> IngestSendGridAsync(JsonElement root, CancellationToken ct = default);

        /// <summary>Ingerisce il payload del webhook eventi di Brevo (oggetto o array JSON).</summary>
        Task<int> IngestBrevoAsync(JsonElement root, CancellationToken ct = default);
    }
}
