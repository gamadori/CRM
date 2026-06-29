using CRM.Shared;
using CRM.Shared.DTOs;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CRM.Server.Services
{
    public class RsaLicenseService : IRsaLicenseService
    {
        private readonly RSA _rsa;
        private readonly ILogger<RsaLicenseService> _logger;

        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public RsaLicenseService(IConfiguration config, ILogger<RsaLicenseService> logger)
        {
            _logger = logger;
            _rsa = RSA.Create(2048);

            var pem = config["License:PrivateKeyPem"];
            if (!string.IsNullOrWhiteSpace(pem))
            {
                _rsa.ImportFromPem(pem.AsSpan());
            }
            else
            {
                // Development only: log chiave generata al volo
                _logger.LogWarning("License:PrivateKeyPem non configurato. Generata chiave RSA temporanea (non persistente).");
                _logger.LogWarning("CHIAVE PUBBLICA (distribuire alle macchine):\n{PubKey}", _rsa.ExportRSAPublicKeyPem());
                _logger.LogWarning("CHIAVE PRIVATA (aggiungere a appsettings License:PrivateKeyPem):\n{PrivKey}", _rsa.ExportRSAPrivateKeyPem());
            }
        }

        public LicenseFilePayload GenerateLicense(ArticleLicense license, string serialNumber)
        {
            var features = license.Features?
                .Where(f => f.FeatureDef != null)
                .ToDictionary(f => f.FeatureDef.Key, f => f.IsEnabled ? f.Value : "disabled")
                ?? new Dictionary<string, string>();

            var payload = new LicenseFilePayload
            {
                SerialNumber = serialNumber,
                MachineKey = license.MachineKey ?? string.Empty,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = license.ExpirationDate,
                IsActive = license.IsActive,
                Features = features,
                Signature = string.Empty
            };

            payload.Signature = Sign(payload);
            return payload;
        }

        public bool VerifyLicense(LicenseFilePayload payload)
        {
            try
            {
                var content = BuildSigningContent(payload);
                var bytes = Encoding.UTF8.GetBytes(content);
                var sigBytes = Convert.FromBase64String(payload.Signature);
                return _rsa.VerifyData(bytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }

        public string ExportPublicKeyPem() => _rsa.ExportRSAPublicKeyPem();

        private string Sign(LicenseFilePayload payload)
        {
            var content = BuildSigningContent(payload);
            var bytes = Encoding.UTF8.GetBytes(content);
            var sig = _rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sig);
        }

        // Produce una stringa deterministica di tutto ciò che viene firmato (senza Signature).
        private static string BuildSigningContent(LicenseFilePayload p)
        {
            var obj = new
            {
                p.SerialNumber,
                p.MachineKey,
                IssuedAt = p.IssuedAt.ToString("O"),
                ExpiresAt = p.ExpiresAt?.ToString("O"),
                p.IsActive,
                Features = p.Features.OrderBy(kv => kv.Key)
                                     .ToDictionary(kv => kv.Key, kv => kv.Value)
            };
            return JsonSerializer.Serialize(obj, _jsonOpts);
        }
    }
}
