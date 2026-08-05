using CRM.Shared.DTOs;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    /// <summary>
    /// Provider intercambiabile per la lettura dei biglietti da visita. Gemello di
    /// <see cref="IReceiptAnalyzer"/>: stessa astrazione, stesso motivo - il fornitore cambia,
    /// il resto dell'applicazione no.
    /// </summary>
    public interface IBusinessCardAnalyzer
    {
        Task<BusinessCardExtractionResult> AnalyzeAsync(byte[] fileBytes, string fileName);
    }
}
