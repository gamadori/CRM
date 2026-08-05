using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum MachineBackupOwnerType
    {
        Product = 1,
        Article = 2
    }

    public enum MachineBackupSource
    {
        Manual = 1,
        MachineApi = 2
    }

    // Il permesso della chiave di backup e' ora ApiKeyPermission, condiviso con gli altri ambiti.

    public class MachineBackup
    {
        [Key]
        public int Id { get; set; }

        public MachineBackupOwnerType OwnerType { get; set; }

        [ForeignKey(nameof(Product))]
        public int? IdProduct { get; set; }

        [ForeignKey(nameof(Article))]
        public int? IdArticle { get; set; }

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long Size { get; set; }

        [Required, MaxLength(64)]
        public string Sha256 { get; set; } = string.Empty;

        public int Version { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public MachineBackupSource Source { get; set; } = MachineBackupSource.Manual;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? ExternalReference { get; set; }

        [MaxLength(450)]
        public string? CreatedBy { get; set; }

        public Product? Product { get; set; }

        public Article? Article { get; set; }
    }

    // La chiave dei backup non vive piu' qui: e' confluita in ApiKey (ambito MachineBackup),
    // insieme a quelle dei ticket esterni e dell'app fiera. Erano tre tabelle con gli stessi
    // campi e tre punti di verifica da ricordare di correggere insieme.
}
