using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IArticleEventService
    {
        Task<int> AddEventAsync(int itemId, AddItemEventRequest req, string? actorUserId = null);

        Task<AvailableEventsResponse> GetAvailableEventsAsync(int itemId, int domainId);

        Task<List<ArticleEventDto>> Timeline(int itemId);

        Task<List<DomainStateDto>> DomainStates(int itemId);
    }
}
