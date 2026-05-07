using CRM.Shared;
using CRM.Shared.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public interface IExpenseReceiptService
    {
        /// <summary>
        /// Ottiene tutte le note spese di un intervento
        /// </summary>
        Task<List<ExpenseReceiptDTO>> GetByInterventionIdAsync(int interventionId);

        /// <summary>
        /// Ottiene il riepilogo delle note spese di un intervento
        /// </summary>
        Task<ExpenseReceiptSummaryDTO> GetSummaryByInterventionIdAsync(int interventionId);

        /// <summary>
        /// Ottiene una singola nota spese per ID
        /// </summary>
        Task<ExpenseReceiptDTO> GetByIdAsync(int id);

        /// <summary>
        /// Crea una nuova nota spese
        /// </summary>
        Task<ExpenseReceiptDTO> CreateAsync(ExpenseReceiptCreateUpdateDTO dto, string userId);

        /// <summary>
        /// Aggiorna una nota spese esistente
        /// </summary>
        Task<ExpenseReceiptDTO> UpdateAsync(int id, ExpenseReceiptCreateUpdateDTO dto, string userId);

        /// <summary>
        /// Elimina una nota spese
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Conferma i dati estratti di una nota spese
        /// </summary>
        Task<bool> ConfirmAsync(int id, string userId);

        /// <summary>
        /// Crea una nota spese dal risultato dell'estrazione Azure
        /// </summary>
        Task<ExpenseReceiptDTO> CreateFromExtractionAsync(
            int interventionId, 
            int attachmentFileId, 
            ReceiptExtractionResult extractionResult, 
            string userId);
    }
}
