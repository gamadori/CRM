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

    public enum MachineParameterApiKeyPermission
    {
        ReadOnly = 1,
        ReadWrite = 2
    }

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

    public class MachineParameterApiKey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string KeyHash { get; set; } = string.Empty;

        public string? KeyPrefix { get; set; }

        public MachineParameterApiKeyPermission Permission { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public string? Notes { get; set; }
    }
}
