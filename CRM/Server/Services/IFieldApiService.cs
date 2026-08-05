using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    /// <summary>
    /// Il ponte fra l'app di cattura biglietti e il CRM.
    /// <para>
    /// Le chiavi non si gestiscono qui: le governa <see cref="IApiKeyService"/> per tutti gli
    /// ambiti. Questo servizio riceve una chiave gia' verificata e si occupa di fiere e biglietti.
    /// </para>
    /// </summary>
    public interface IFieldApiService
    {
        /// <summary>Fiere e campagne fra cui l'app fa scegliere: elenco breve, non tutto lo storico.</summary>
        Task<List<FieldInitiativeDTO>> GetInitiativesAsync();

        Task<FieldLeadResponse> CreateLeadAsync(ApiKey apiKey, FieldLeadRequest request, byte[]? photo, string? photoFileName);
    }
}
