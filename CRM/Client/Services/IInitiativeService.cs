using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IInitiativeService : IDataService<Initiative, InitiativeDTO, int, InitiativeFilter, decimal>
    {
        /// <summary>Il resoconto dell'iniziativa: costi, cosa e' successo, cosa si e' aperto.</summary>
        Task<InitiativeSummaryDTO?> GetReportAsync(int id);

        /// <summary>I biglietti raccolti, con cosa manca e a quale azienda gia' nota somigliano.</summary>
        Task<List<InitiativeLeadTriageDTO>> GetLeadTriageAsync(int id);

        Task<bool> LinkLeadToCompanyAsync(int id, int idLead, int idCompany);

        /// <summary>Chi e' impegnato in un'iniziativa nel periodo indicato.</summary>
        Task<List<UserAwayDTO>> GetAwayUsersAsync(DateTime from, DateTime to);

        Task<List<InitiativeScheduleDTO>> GetSchedulesAsync(int id);

        Task<APIResponseMessage<InitiativeScheduleDTO>> SaveScheduleAsync(int id, InitiativeScheduleDTO schedule);

        Task<bool> DeleteScheduleAsync(int id, int idSchedule);

        Task<APIResponseMessage<InitiativeDTO>> CloseAsync(int id, string? closingNotes);
    }
}
