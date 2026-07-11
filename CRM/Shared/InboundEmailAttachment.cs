using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    /// <summary>Allegato di un'email in ingresso, memorizzato con il contenuto per il download.</summary>
    public class InboundEmailAttachment
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(InboundEmail))]
        public int IdInboundEmail { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string? ContentType { get; set; }

        public long Size { get; set; }

        public byte[] Content { get; set; } = Array.Empty<byte>();

        public virtual InboundEmail? InboundEmail { get; set; }
    }

    /// <summary>Metadati di un allegato (senza il contenuto), per liste e viste.</summary>
    public class InboundEmailAttachmentInfo
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Size { get; set; }
    }
}
