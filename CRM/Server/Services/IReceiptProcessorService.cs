using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per l'elaborazione automatica di scontrini e fatture tramite Azure Form Recognizer
    /// </summary>
    public interface IReceiptProcessorService
    {
        /// <summary>
        /// Elabora un file allegato (scontrino/fattura) ed estrae i campi strutturati
        /// </summary>
        /// <param name="attachmentFileId">ID del file attachment caricato</param>
        /// <param name="useCustomModel">Se true, usa modello custom invece di prebuilt-receipt</param>
        /// <param name="customModelId">ID del modello custom (opzionale)</param>
        /// <returns>Risultato dell'estrazione con campi estratti</returns>
        Task<ReceiptExtractionResult> ProcessReceiptAsync(int attachmentFileId, bool useCustomModel = false, string customModelId = null);

        /// <summary>
        /// Elabora un file da byte array direttamente
        /// </summary>
        /// <param name="fileBytes">Contenuto del file</param>
        /// <param name="fileName">Nome file (per determinare content-type)</param>
        /// <param name="useCustomModel">Se true, usa modello custom</param>
        /// <param name="customModelId">ID modello custom</param>
        /// <returns>Risultato dell'estrazione</returns>
        Task<ReceiptExtractionResult> ProcessReceiptFromBytesAsync(byte[] fileBytes, string fileName, bool useCustomModel = false, string customModelId = null);

        /// <summary>
        /// Verifica la connettività con Azure Form Recognizer
        /// </summary>
        /// <returns>True se connesso correttamente</returns>
        Task<bool> HealthCheckAsync();
    }
}
