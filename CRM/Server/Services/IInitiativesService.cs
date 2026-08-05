using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface IInitiativesService
    {
        Task<PagingResponse<InitiativeDTO, decimal>?> GetSummaryListAsync(InitiativeFilter? args);
        Task<List<InitiativeDTO>?> GetListAsync(InitiativeFilter? args = null);
        Task<InitiativeDTO?> GetItemAsync(int id);

        /// <summary>Il resoconto: costi, cosa e' successo, cosa si e' aperto.</summary>
        Task<InitiativeSummaryDTO?> GetReportAsync(int id);

        /// <summary>I biglietti raccolti, con cosa manca e a quale azienda gia' nota somigliano.</summary>
        Task<List<InitiativeLeadTriageDTO>> GetLeadTriageAsync(int id);

        /// <summary>Collega un lead a un'azienda esistente (esito del triage).</summary>
        Task<bool> LinkLeadToCompanyAsync(int id, int idLead, int idCompany);

        /// <summary>Chi e' impegnato in un'iniziativa nel periodo indicato, e con quale.</summary>
        Task<List<UserAwayDTO>> GetAwayUsersAsync(DateTime from, DateTime to);

        Task<List<InitiativeScheduleDTO>> GetSchedulesAsync(int id);

        Task<APIResponseMessage<InitiativeScheduleDTO>> SaveScheduleAsync(int id, InitiativeScheduleDTO schedule);

        Task<bool> DeleteScheduleAsync(int id, int idSchedule);

        Task<APIResponseMessage<InitiativeDTO>> PostAsync(Initiative item);
        Task<APIResponseMessage<InitiativeDTO>> CloseAsync(int id, string? closingNotes);
        Task<bool> DeleteAsync(int id);
    }
}
