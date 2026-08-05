using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    /// <summary>
    /// I ticket aperti da un cliente esterno tramite API.
    /// <para>
    /// La gestione delle chiavi non sta piu' qui: e' passata a <see cref="IApiKeyService"/>, che le
    /// governa per tutti gli ambiti. Questo servizio riceve una chiave gia' verificata e si occupa
    /// solo dei ticket.
    /// </para>
    /// </summary>
    public interface IExternalTicketApiService
    {
        Task<ExternalTicketResponse> CreateTicketAsync(ApiKey apiKey, ExternalTicketCreateRequest request);

        Task<ExternalTicketResponse?> GetTicketAsync(ApiKey apiKey, int id);

        Task<List<ExternalTicketResponse>> GetTicketsAsync(ApiKey apiKey, bool includeClosed, int skip, int top);
    }
}
