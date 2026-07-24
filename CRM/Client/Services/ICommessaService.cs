using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ICommessaService : IDataService<Commessa, CommessaDTO, int, CommessaFilter, int>
    {
        Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state);

        /// <summary>Avvia produzione: crea le commesse (una per unità) dalla riga d'ordine.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId);

        Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId);

        Task<List<CommessaDTO>> GetByOrderAsync(int orderId);
    }
}
