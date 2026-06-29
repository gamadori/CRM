using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IRsaLicenseService
    {
        LicenseFilePayload GenerateLicense(ArticleLicense license, string serialNumber);
        bool VerifyLicense(LicenseFilePayload payload);
        string ExportPublicKeyPem();
    }
}
