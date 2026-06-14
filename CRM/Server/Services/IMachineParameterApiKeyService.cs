using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IMachineParameterApiKeyService
    {
        Task<List<MachineParameterApiKeyDTO>> GetListAsync();
        Task<MachineParameterApiKeyCreateResponse> CreateAsync(MachineParameterApiKeyCreateRequest request);
        Task<bool> RevokeAsync(int id);
        Task<MachineParameterApiKey?> ValidateAsync(string? plainTextKey, MachineParameterApiKeyPermission requiredPermission);
    }
}
