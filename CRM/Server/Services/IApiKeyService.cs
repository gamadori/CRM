using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    /// <summary>
    /// Le chiavi di accesso all'API, per tutti gli usi che non passano dal login.
    /// <para>
    /// Punto di verifica unico: prima erano tre, quasi identici, e una correzione andava ricordata
    /// tre volte.
    /// </para>
    /// </summary>
    public interface IApiKeyService
    {
        Task<List<ApiKeyDTO>> GetAsync(ApiKeyScope? scope = null);

        Task<ApiKeyCreateResponse> CreateAsync(ApiKeyCreateRequest request);

        Task<bool> RevokeAsync(int id);

        /// <summary>
        /// Verifica una chiave <b>nel suo ambito</b>. Una chiave valida ma di un altro ambito viene
        /// rifiutata: e' cio' che impedisce a quella di una macchina di scrivere lead e a quella di
        /// un telefono di leggere i backup.
        /// </summary>
        Task<ApiKey?> ValidateAsync(string? plainTextKey, ApiKeyScope scope, ApiKeyPermission? required = null);
    }
}
