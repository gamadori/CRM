using CRM.Client.Models;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ICommesseService
    {
        Task<CommessaDTO?> GetItemAsync(int id);

        Task<PagingResponse<CommessaDTO, int>?> GetSummaryAsync(CommessaFilter? args);

        Task<List<CommessaDTO>?> GetListAsync(CommessaFilter? args = null);

        Task<List<CommessaDTO>> GetByOrderAsync(int orderId);

        Task<APIResponseMessage<CommessaDTO>> PostAsync(Commessa item);

        Task<APIResponseMessage<CommessaDTO>> ChangeStateAsync(int id, CommessaStates state);

        /// <summary>Avvia produzione da una riga d'ordine: crea una commessa per unità clonando le
        /// fasi dal template del prodotto, con schedulazione all'indietro dalla consegna.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartProductionAsync(int orderRowId);

        /// <summary>Avvia produzione interna (magazzino, prototipi, ricambi, rilavorazioni): crea
        /// commesse senza riga d'ordine, schedulate all'indietro dalla data obiettivo indicata.</summary>
        Task<APIResponseMessage<List<CommessaDTO>>> StartInternalProductionAsync(InternalProductionRequestDTO req);

        /// <summary>Conferma "pronto" una riga senza produzione (nessuna commessa).</summary>
        Task<APIResponseMessage<CommessaDTO>> ConfirmRowReadyAsync(int orderRowId);

        /// <summary>Elimina la commessa e le sue fasi. Il Code distingue non trovata,
        /// non accessibile ed errore, cosi' il controller puo' rispondere con lo stato giusto.</summary>
        Task<APIResponseMessage<bool>> DeleteAsync(int id);
    }
}
