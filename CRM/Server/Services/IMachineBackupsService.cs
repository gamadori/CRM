using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IMachineBackupsService
    {
        Task<MachineBackupListDTO> GetListAsync(MachineBackupFilter filter);
        Task<MachineBackupDTO?> GetLatestAsync(MachineBackupOwnerType ownerType, int ownerId);
        Task<MachineBackupDTO?> GetAsync(int id);
        Task<MachineBackupDTO> UploadAsync(
            MachineBackupOwnerType ownerType,
            int ownerId,
            string fileName,
            string contentType,
            Stream content,
            MachineBackupUploadMetadata metadata,
            MachineBackupSource source,
            string? createdBy,
            CancellationToken cancellationToken = default);
        Task<(Stream Content, string ContentType, string FileName)?> DownloadAsync(int id);
        Task<bool> DeleteAsync(int id);
    }
}
