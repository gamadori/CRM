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

        /// <summary>
        /// Allega un file a un ticket dell'azienda della chiave. Restituisce <c>null</c> se il
        /// ticket non esiste o non appartiene a quell'azienda.
        /// </summary>
        Task<ExternalTicketAttachmentResponse?> AttachFileAsync(ApiKey apiKey, int idTicket, string fileName, string contentType, byte[] content, string? description);
    }
}
