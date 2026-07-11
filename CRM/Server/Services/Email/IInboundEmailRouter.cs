namespace CRM.Server.Services.Email
{
    /// <summary>Allegato normalizzato di un messaggio in ingresso.</summary>
    public sealed class InboundAttachment
    {
        public string FileName { get; init; } = "allegato";
        public string? ContentType { get; init; }
        public byte[] Content { get; init; } = Array.Empty<byte>();
    }

    /// <summary>Messaggio in ingresso normalizzato, indipendente dalla sorgente (IMAP o webhook ESP).</summary>
    public sealed class InboundMessage
    {
        public int InboxId { get; init; }
        public string? MessageId { get; init; }
        public long? Uid { get; init; }
        public string? FromAddress { get; init; }
        public string? FromName { get; init; }
        public string? ToAddress { get; init; }
        public string? Subject { get; init; }
        public string? Body { get; init; }
        public DateTime ReceivedAt { get; init; } = DateTime.Now;
        public IReadOnlyList<InboundAttachment> Attachments { get; init; } = Array.Empty<InboundAttachment>();
    }

    /// <summary>
    /// Instrada un messaggio in ingresso verso il CRM: deduplica, risolve il mittente e — secondo
    /// l'azione della casella — crea l'attività sulla timeline. Unico punto a valle per entrambe le
    /// modalità di ricezione.
    /// </summary>
    public interface IInboundEmailRouter
    {
        /// <summary>Ingerisce il messaggio; ritorna true se è stato registrato (false se duplicato/ignorato).</summary>
        Task<bool> IngestAsync(InboundMessage message, CancellationToken ct = default);
    }
}
