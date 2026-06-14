using System;
using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class MachineBackupDTO
    {
        public int Id { get; set; }
        public MachineBackupOwnerType OwnerType { get; set; }
        public int? IdProduct { get; set; }
        public int? IdArticle { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public MachineBackupSource Source { get; set; }
        public string? Description { get; set; }
        public string? ExternalReference { get; set; }
        public string? CreatedBy { get; set; }
    }

    public class MachineBackupFilter
    {
        public MachineBackupOwnerType OwnerType { get; set; }
        public int OwnerId { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; } = 50;
    }

    public class MachineBackupListDTO
    {
        public List<MachineBackupDTO> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class MachineBackupUploadMetadata
    {
        public string? Description { get; set; }
        public string? ExternalReference { get; set; }
    }

    public class MachineParameterApiKeyDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? KeyPrefix { get; set; }
        public MachineParameterApiKeyPermission Permission { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class MachineParameterApiKeyCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public MachineParameterApiKeyPermission Permission { get; set; } = MachineParameterApiKeyPermission.ReadOnly;
        public DateTime? ExpiresAt { get; set; }
        public string? Notes { get; set; }
    }

    public class MachineParameterApiKeyCreateResponse
    {
        public MachineParameterApiKeyDTO ApiKey { get; set; } = new();
        public string PlainTextKey { get; set; } = string.Empty;
    }

    public class MachineArticleListFilter
    {
        public int? IdProduct { get; set; }
        public string? ProductCode { get; set; }
        public string? SerialNumber { get; set; }
        public string? Search { get; set; }
        public int? Skip { get; set; }
        public int? Take { get; set; } = 50;
    }

    public class MachineArticleDTO
    {
        public int Id { get; set; }
        public int? IdProduct { get; set; }
        public string? ProductCode { get; set; }
        public string? ProductName { get; set; }
        public string? SerialNumber { get; set; }
        public string? Name { get; set; }
        public int Year { get; set; }
        public string? CompanyName { get; set; }
        public string? CompleteName { get; set; }
        public DateTime? LastBackupAt { get; set; }
    }
}
