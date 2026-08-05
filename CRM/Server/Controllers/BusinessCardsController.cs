using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Lettura dei biglietti da visita raccolti in fiera.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BusinessCardsController : ControllerBase
    {
        private const long MaxBytes = 10 * 1024 * 1024;

        private readonly IBusinessCardAnalyzer _analyzer;
        private readonly ILogger<BusinessCardsController> _logger;

        public BusinessCardsController(IBusinessCardAnalyzer analyzer, ILogger<BusinessCardsController> logger)
        {
            _analyzer = analyzer;
            _logger = logger;
        }

        /// <summary>
        /// Legge una foto di biglietto e restituisce i campi riconosciuti.
        /// <para>
        /// Risponde sempre 200, anche quando non riesce a leggere niente: allo stand un errore HTTP
        /// diventerebbe un messaggio rosso davanti alla persona che hai di fronte, mentre qui si
        /// compila a mano e si va avanti. L'esito sta in <c>Success</c>.
        /// </para>
        /// </summary>
        [HttpPost("analyze")]
        [ProducesResponseType(typeof(BusinessCardExtractionResult), 200)]
        public async Task<ActionResult<BusinessCardExtractionResult>> Analyze(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return Ok(new BusinessCardExtractionResult { Success = false, ErrorMessage = "Nessuna foto ricevuta." });

            if (file.Length > MaxBytes)
                return Ok(new BusinessCardExtractionResult { Success = false, ErrorMessage = "Foto troppo grande. Massimo 10 MB." });

            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);

                return Ok(await _analyzer.AnalyzeAsync(memoryStream.ToArray(), file.FileName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lettura biglietto non riuscita");

                return Ok(new BusinessCardExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Lettura non riuscita: compila i campi a mano, la foto resta allegata."
                });
            }
        }
    }
}
