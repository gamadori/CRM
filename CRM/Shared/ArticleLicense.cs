using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Shared
{
    public enum LicenseFeatureValueType { Bool, Int, String }

    public class ArticleLicenseFeatureDef
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; }

        [Required]
        [MaxLength(200)]
        public string Label { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public LicenseFeatureValueType ValueType { get; set; } = LicenseFeatureValueType.Bool;

        [MaxLength(200)]
        public string DefaultValue { get; set; } = "false";

        [ForeignKey("ProductType")]
        public int? IdProductType { get; set; }

        [ForeignKey("Product")]
        public int? IdProduct { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ProductType? ProductType { get; set; }

        public virtual Product? Product { get; set; }
    }

    public class ArticleLicense
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Article")]
        public int IdArticle { get; set; }

        /// <summary>Hash fingerprint inviato dalla macchina al primo avvio.</summary>
        [MaxLength(256)]
        public string? MachineKey { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpirationDate { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public virtual Article Article { get; set; }

        public virtual ICollection<ArticleLicenseFeature> Features { get; set; } = new List<ArticleLicenseFeature>();
    }

    public class ArticleLicenseFeature
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("License")]
        public int IdLicense { get; set; }

        [ForeignKey("FeatureDef")]
        public int IdFeatureDef { get; set; }

        [MaxLength(500)]
        public string Value { get; set; } = "false";

        public bool IsEnabled { get; set; } = true;

        public virtual ArticleLicense License { get; set; }

        public virtual ArticleLicenseFeatureDef FeatureDef { get; set; }
    }
}
