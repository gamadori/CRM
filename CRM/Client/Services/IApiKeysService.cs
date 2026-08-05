using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>Chiavi API di ogni ambito: backup macchina, ticket esterni, app fiera.</summary>
    public interface IApiKeysService
    {
        Task<List<ApiKeyDTO>> GetAsync(ApiKeyScope? scope = null);

        /// <summary>La chiave in chiaro torna solo qui: dopo resta solo il prefisso.</summary>
        Task<(ApiKeyCreateResponse? Response, string? Error)> CreateAsync(ApiKeyCreateRequest request);

        Task<bool> RevokeAsync(int id);
    }
}
