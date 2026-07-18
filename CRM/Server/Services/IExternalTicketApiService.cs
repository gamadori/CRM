using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IExternalTicketApiService
    {
        Task<List<ExternalTicketApiKeyDTO>> GetApiKeysAsync();

        Task<ExternalTicketApiKeyCreateResponse> CreateApiKeyAsync(ExternalTicketApiKeyCreateRequest request);

        Task<bool> RevokeApiKeyAsync(int id);

        Task<ExternalTicketApiKey?> ValidateApiKeyAsync(string? plainTextKey);

        Task<ExternalTicketResponse> CreateTicketAsync(ExternalTicketApiKey apiKey, ExternalTicketCreateRequest request);

        Task<ExternalTicketResponse?> GetTicketAsync(ExternalTicketApiKey apiKey, int id);

        Task<List<ExternalTicketResponse>> GetTicketsAsync(ExternalTicketApiKey apiKey, bool includeClosed, int skip, int top);
    }
}
