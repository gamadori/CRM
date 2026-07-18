using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public class ExternalTicketApiKey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? KeyPrefix { get; set; }

        [ForeignKey(nameof(Company))]
        public int IdCompany { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public virtual Company? Company { get; set; }
    }
}
