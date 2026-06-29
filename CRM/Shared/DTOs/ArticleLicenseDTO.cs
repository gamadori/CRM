using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CRM.Shared.DTOs
{
    public class ArticleLicenseFeatureDefDTO
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Label { get; set; }
        public string? Description { get; set; }
        public LicenseFeatureValueType ValueType { get; set; }
        public string DefaultValue { get; set; }
        public int? IdProductType { get; set; }
        public string? ProductTypeName { get; set; }
        public int? IdProduct { get; set; }
        public string? ProductName { get; set; }
        public bool IsActive { get; set; }
    }

    public class ArticleLicenseFeatureDTO
    {
        public int Id { get; set; }
        public int IdLicense { get; set; }
        public int IdFeatureDef { get; set; }
        public string FeatureKey { get; set; }
        public string FeatureLabel { get; set; }
        public string? FeatureDescription { get; set; }
        public LicenseFeatureValueType ValueType { get; set; }
        public string Value { get; set; } = "false";
        public bool IsEnabled { get; set; } = true;
    }

    public class ArticleLicenseDTO
    {
        public int Id { get; set; }
        public int IdArticle { get; set; }
        public string SerialNumber { get; set; }
        public string? MachineKey { get; set; }
        public bool MachineRegistered => !string.IsNullOrEmpty(MachineKey);
        public DateTime StartDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<ArticleLicenseFeatureDTO> Features { get; set; } = new();

        public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.UtcNow;
        public bool IsValid => IsActive && !IsExpired;
    }

    /// <summary>Payload del file .lic generato dal server e verificato dalla macchina.</summary>
    public class LicenseFilePayload
    {
        public string SerialNumber { get; set; }
        public string MachineKey { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Key=FeatureKey, Value=valore stringa ("true"/"false"/numero).</summary>
        public Dictionary<string, string> Features { get; set; } = new();
        /// <summary>Firma RSA-SHA256 in Base64 di tutti i campi precedenti.</summary>
        public string Signature { get; set; }
    }

    public class MachineRegistrationRequest
    {
        public string SerialNumber { get; set; }
        public string MachineKey { get; set; }
    }

    public class MachineRegistrationResponse
    {
        public bool Success { get; set; }
        public bool LicenseAvailable { get; set; }
        public LicenseFilePayload? License { get; set; }
        public string? Message { get; set; }
    }

    public class ArticleLicenseSaveRequest
    {
        public int IdArticle { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }
        public List<ArticleLicenseFeatureDTO> Features { get; set; } = new();
    }
}
