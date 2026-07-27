using CRM.Client.Models;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
    public interface ICommessaFasiService
    {
        Task<List<CommessaFaseDTO>?> GetTreeAsync(int idCommessa);

        /// <summary>Singola fase con il codice della commessa di appartenenza.</summary>
        Task<CommessaFaseDTO?> GetItemAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDTO>> SaveAsync(CommessaFaseDTO dto);

        Task<bool> BulkSaveAsync(List<CommessaFaseDTO> dtos);

        Task<bool> DeleteAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseDependencyDTO>> AddDependencyAsync(CommessaFaseDependencyDTO dto);

        Task<bool> RemoveDependencyAsync(int dependencyId);

        /// <summary>Ricalcola l'avanzamento di una fase dai suoi ticket (chiusi/totali) e propaga
        /// alla commessa. No-op se faseId è null.</summary>
        Task RecomputeFaseProgressAsync(int? faseId);

        /// <summary>True se l'utente corrente puo' prendere in carico la fase: Standard e Client
        /// solo se appartengono al gruppo abilitato, Admin e SuperUser sempre.</summary>
        Task<bool> CanTakeFaseAsync(int faseId);

        /// <summary>Nomi dei predecessori che impediscono l'avvio della fase (vuota = avviabile).
        /// Vale solo per le fasi ancora Pending: quelle in lavorazione non si ricongelano.</summary>
        Task<List<string>> GetStartBlockersAsync(int faseId);

        Task<APIResponseMessage<CommessaFaseTicketPlanDTO>> GenerateTicketFromPlanAsync(int ticketPlanId);
    }
}
