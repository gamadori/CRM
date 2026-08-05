using System;
using System.ComponentModel.DataAnnotations;

namespace CRM.Shared.DTOs
{
    public class ApiKeyDTO
    {
        public int Id { get; set; }

        public ApiKeyScope Scope { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? KeyPrefix { get; set; }

        public ApiKeyPermission Permission { get; set; }

        public int? IdCompany { get; set; }

        public string? CompanyName { get; set; }

        public string? IdUser { get; set; }

        public string? UserName { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }

        public string? Notes { get; set; }

        /// <summary>Scaduta pur essendo ancora attiva: nell'elenco va distinta da una revocata.</summary>
        public bool IsExpired => ExpiresAt != null && ExpiresAt.Value < DateTime.UtcNow;

        public bool IsUsable => IsActive && !IsExpired;

        /// <summary>A chi e' intestata, in chiaro: azienda, persona o niente secondo l'ambito.</summary>
        public string Holder => Scope switch
        {
            ApiKeyScope.ExternalTicket => string.IsNullOrWhiteSpace(CompanyName) ? "-" : CompanyName,
            ApiKeyScope.Field => string.IsNullOrWhiteSpace(UserName) ? "-" : UserName,
            _ => "-"
        };
    }

    public class ApiKeyCreateRequest
    {
        public ApiKeyScope Scope { get; set; } = ApiKeyScope.Field;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Usato solo nell'ambito backup; altrove resta ReadWrite.</summary>
        public ApiKeyPermission Permission { get; set; } = ApiKeyPermission.ReadWrite;

        /// <summary>Obbligatoria per l'ambito ticket esterni.</summary>
        public int? IdCompany { get; set; }

        /// <summary>Obbligatorio per l'ambito fiera.</summary>
        public string? IdUser { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }

    public class ApiKeyCreateResponse
    {
        public ApiKeyDTO? ApiKey { get; set; }

        /// <summary>
        /// La chiave in chiaro. Esiste solo in questa risposta: sul database ne resta l'impronta,
        /// quindi se non viene copiata adesso l'unica strada e' generarne un'altra.
        /// </summary>
        public string PlainTextKey { get; set; } = string.Empty;
    }
}
