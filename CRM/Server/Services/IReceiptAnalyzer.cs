using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>Provider intercambiabile per l'analisi strutturata di scontrini e fatture.</summary>
    public interface IReceiptAnalyzer
    {
        Task<ReceiptExtractionResult> AnalyzeAsync(
            byte[] fileBytes,
            string fileName,
            bool useCustomModel = false,
            string customModelId = null);

        Task<bool> HealthCheckAsync();
    }
}
