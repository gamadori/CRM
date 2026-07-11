using CRM.Shared.DTOs;

namespace CRM.Server.Services.Email
{
    /// <summary>
    /// Traduce oggetto e corpo di un template email da una lingua sorgente a più lingue target,
    /// preservando i segnaposto (<c>$NAME</c>, ...) e la struttura HTML. Basato su Claude, non
    /// bloccante: senza API key o in errore ritorna lista vuota.
    /// </summary>
    public interface IEmailTemplateTranslator
    {
        bool IsAvailable { get; }

        Task<IReadOnlyList<EmailTemplateVersionDTO>> TranslateAsync(
            string sourceLanguage, string subject, string? body,
            IReadOnlyList<string> targetLanguages, CancellationToken ct = default);
    }
}
