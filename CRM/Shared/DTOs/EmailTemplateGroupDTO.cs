using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Un template email visto come singola unità per <see cref="Tipo"/>, con tutte le sue versioni
    /// linguistiche insieme. Sotto rimane la memorizzazione normalizzata (una riga per lingua), ma
    /// admin lo gestisce come un template solo.
    /// </summary>
    public class EmailTemplateGroupDTO
    {
        public EmailsTypes Tipo { get; set; }

        /// <summary>Logo condiviso da tutte le lingue del template.</summary>
        public int? IdLogo { get; set; }

        public List<EmailTemplateVersionDTO> Versions { get; set; } = new();
    }

    /// <summary>Versione linguistica (oggetto + corpo) di un template.</summary>
    public class EmailTemplateVersionDTO
    {
        public int Id { get; set; }

        public string Language { get; set; } = "";

        public string? Subject { get; set; }

        public string? Body { get; set; }
    }

    /// <summary>Richiesta di traduzione automatica di una versione verso altre lingue.</summary>
    public class EmailTemplateTranslateRequest
    {
        public string SourceLanguage { get; set; } = "it";

        public string Subject { get; set; } = "";

        public string? Body { get; set; }

        public List<string> TargetLanguages { get; set; } = new();
    }
}
