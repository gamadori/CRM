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
        /// Ottiene tutte le note spese di un'attivita' (visita commerciale o di cortesia).
        /// </summary>
        Task<List<ExpenseReceiptDTO>> GetByActivityIdAsync(int activityId);

        /// <summary>
        /// Ottiene il riepilogo delle note spese di un'attivita'.
        /// </summary>
        Task<ExpenseReceiptSummaryDTO> GetSummaryByActivityIdAsync(int activityId);

        /// <summary>
        /// Note spese di un'iniziativa (fiera, trasferta): e' la cartella delle spese di quella
        /// occasione, quella che risponde a "quanto ci e' costata".
        /// </summary>
        /// <param name="restrictToUserId">
        /// Valorizzato per chi puo' vedere solo le proprie spese; null per chi le vede tutte.
        /// Il vincolo si applica qui e non nella pagina: e' l'unico punto da cui passano le letture.
        /// </param>
        Task<List<ExpenseReceiptDTO>> GetByInitiativeIdAsync(int initiativeId, string restrictToUserId);

        /// <summary>Riepilogo delle note spese di un'iniziativa, con lo stesso vincolo di visibilita'.</summary>
        Task<ExpenseReceiptSummaryDTO> GetSummaryByInitiativeIdAsync(int initiativeId, string restrictToUserId);

        /// <summary>
        /// Elenco trasversale filtrato: e' quello che rende le note spese consultabili senza
        /// dover scendere dentro un intervento.
        /// </summary>
        /// <param name="restrictToUserId">
        /// Valorizzato per chi puo' vedere solo le proprie spese; null per chi le vede tutte.
        /// </param>
        Task<(List<ExpenseReceiptDTO> Items, int TotalCount)> SearchAsync(
            ExpenseReceiptFilter filter, string restrictToUserId);

        /// <summary>
        /// Totali sull'intero insieme filtrato (non sulla pagina), con lo spaccato per valuta e
        /// per persona e il conteggio di quelle ancora da convertire.
        /// </summary>
        Task<ExpenseReceiptSummaryDTO> GetSummaryAsync(
            ExpenseReceiptFilter filter, string restrictToUserId);

        /// <summary>
        /// Prepara il prospetto delle note spese raggruppate per tipologia (vitto, alloggio,
        /// trasporti...), sullo stesso insieme che l'elenco sta mostrando: stesso filtro, stesso
        /// vincolo di visibilita'. Restituisce i dati gia' calcolati; la stampa e' un'altra cosa.
        /// </summary>
        Task<ExpenseReportData> BuildReportDataAsync(ExpenseReceiptFilter filter, string restrictToUserId);

        /// <summary>
        /// Ottiene una singola nota spese per ID
        /// </summary>
        Task<ExpenseReceiptDTO> GetByIdAsync(int id);

        /// <summary>
        /// Crea una nuova nota spese
        /// </summary>
        Task<ExpenseReceiptDTO> CreateAsync(ExpenseReceiptCreateUpdateDTO dto, string userId);

        Task<List<ExpenseReceiptDTO>> CreateBatchAsync(ExpenseReceiptCreateUpdateDTO dto, string userId);

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
