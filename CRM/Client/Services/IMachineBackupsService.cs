using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IMachineBackupsService
    {
        Task<MachineBackupListDTO> GetListAsync(MachineBackupOwnerType ownerType, int ownerId, int skip = 0, int take = 50);
        Task<MachineBackupDTO?> UploadAsync(
            MachineBackupOwnerType ownerType,
            int ownerId,
            IBrowserFile file,
            string? description,
            string? externalReference,
            IProgress<double>? progress = null);
        Task<bool> DeleteAsync(int id);
    }
}
