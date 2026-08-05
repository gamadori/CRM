using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Endpoint dell'app di cattura biglietti in fiera.
    /// <para>
    /// <see cref="AllowAnonymous"/> perche' l'autenticazione qui non e' quella del CRM: l'app non
    /// fa login interattivo, si presenta con una chiave nell'intestazione <c>X-Api-Key</c> e ogni
    /// azione la verifica prima di fare qualsiasi cosa. E' lo stesso schema di
    /// <see cref="ExternalTicketsController"/>.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/field")]
    public class FieldController : ControllerBase
    {
        private const string ApiKeyHeader = "X-Api-Key";
        private const long MaxPhotoBytes = 12 * 1024 * 1024;

        private readonly IFieldApiService _service;
        private readonly IApiKeyService _apiKeys;
        private readonly IBusinessCardAnalyzer _cardAnalyzer;
        private readonly ILogEventService _logEventService;

        public FieldController(
            IFieldApiService service,
            IApiKeyService apiKeys,
            IBusinessCardAnalyzer cardAnalyzer,
            ILogEventService logEventService)
        {
            _service = service;
            _apiKeys = apiKeys;
            _cardAnalyzer = cardAnalyzer;
            _logEventService = logEventService;
        }

        /// <summary>
        /// Verifica configurazione: conferma che URL e chiave siano giusti e dice a nome di chi si
        /// sta scrivendo. E' la prova che l'app deve poter fare PRIMA della fiera, non allo stand.
        /// </summary>
        [HttpGet("ping")]
        public async Task<ActionResult<FieldPingResponse>> Ping()
        {
            var key = await AuthorizeAsync();
            if (key == null)
                return Unauthorized();

            return Ok(new FieldPingResponse
            {
                Ok = true,
                UserName = key.User?.NameComplete ?? string.Empty,
                KeyName = key.Name,
                ExpiresAt = key.ExpiresAt
            });
        }

        /// <summary>Le fiere fra cui scegliere: alimenta l'elenco a tendina dell'app.</summary>
        [HttpGet("initiatives")]
        public async Task<ActionResult<IEnumerable<FieldInitiativeDTO>>> GetInitiatives()
        {
            var key = await AuthorizeAsync();
            if (key == null)
                return Unauthorized();

            return Ok(await _service.GetInitiativesAsync());
        }

        /// <summary>
        /// Legge un biglietto e restituisce i campi riconosciuti, senza salvare niente.
        /// Risponde 200 anche quando non riesce a leggere: allo stand un errore HTTP diventa un
        /// messaggio rosso davanti alla persona che hai di fronte. L'esito sta in <c>Success</c>.
        /// </summary>
        [HttpPost("cards/analyze")]
        public async Task<ActionResult<BusinessCardExtractionResult>> Analyze(IFormFile file)
        {
            var key = await AuthorizeAsync();
            if (key == null)
                return Unauthorized();

            if (file == null || file.Length == 0)
                return Ok(new BusinessCardExtractionResult { Success = false, ErrorMessage = "Nessuna foto ricevuta." });

            if (file.Length > MaxPhotoBytes)
                return Ok(new BusinessCardExtractionResult { Success = false, ErrorMessage = "Foto troppo grande." });

            try
            {
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory);

                return Ok(await _cardAnalyzer.AnalyzeAsync(memory.ToArray(), file.FileName));
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(FieldController), nameof(Analyze), LogEvent.EventsTypes.Error, ex);
                return Ok(new BusinessCardExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Lettura non riuscita: compila a mano, la foto resta allegata."
                });
            }
        }

        /// <summary>
        /// Invia un biglietto raccolto: dati e foto nella <b>stessa</b> richiesta.
        /// <para>
        /// Volutamente non due chiamate (carica il file, poi crea il lead): da un telefono in fiera
        /// la seconda fallisce spesso, e il risultato sarebbe un allegato orfano o un contatto
        /// senza la sua fonte. Qui o arriva tutto o non arriva niente, e l'app riprova.
        /// </para>
        /// </summary>
        [HttpPost("leads")]
        [RequestSizeLimit(MaxPhotoBytes + (1 * 1024 * 1024))]
        public async Task<ActionResult<FieldLeadResponse>> CreateLead([FromForm] string lead, IFormFile? photo = null)
        {
            var key = await AuthorizeAsync();
            if (key == null)
                return Unauthorized();

            FieldLeadRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<FieldLeadRequest>(lead, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException)
            {
                return BadRequest(new FieldLeadResponse { Ok = false, Message = "Dati del biglietto illeggibili." });
            }

            if (request == null)
                return BadRequest(new FieldLeadResponse { Ok = false, Message = "Dati del biglietto mancanti." });

            if (photo != null && photo.Length > MaxPhotoBytes)
                return BadRequest(new FieldLeadResponse { Ok = false, Message = "Foto troppo grande." });

            byte[]? bytes = null;
            if (photo is { Length: > 0 })
            {
                using var memory = new MemoryStream();
                await photo.CopyToAsync(memory);
                bytes = memory.ToArray();
            }

            var response = await _service.CreateLeadAsync(key, request, bytes, photo?.FileName);

            // Sempre 200 quando il biglietto e' stato accettato, anche se era un doppione: per
            // l'app "e' arrivato" e "era gia' arrivato" hanno lo stesso seguito, togliere
            // l'elemento dalla coda. Un errore la' fuori significa riprovare all'infinito.
            return response.Ok ? Ok(response) : StatusCode(StatusCodes.Status500InternalServerError, response);
        }

        private async Task<ApiKey?> AuthorizeAsync()
        {
            if (!Request.Headers.TryGetValue(ApiKeyHeader, out var header))
                return null;

            return await _apiKeys.ValidateAsync(header.ToString(), ApiKeyScope.Field);
        }
    }
}
