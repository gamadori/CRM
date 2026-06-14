using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IMachineParameterApiKeysService
    {
        Task<List<MachineParameterApiKeyDTO>> GetListAsync();
        Task<MachineParameterApiKeyCreateResponse?> CreateAsync(MachineParameterApiKeyCreateRequest request);
        Task<bool> RevokeAsync(int id);
    }
}
