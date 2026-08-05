using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Gestione delle chiavi API, di ogni ambito.
    /// <para>
    /// Riservato agli amministratori: una chiave permette di operare senza login, a nome di
    /// un'azienda o di una persona. Chi la crea sta delegando un'identita'.
    /// </para>
    /// </summary>
    [Authorize(Policy = "AdminRole")]
    [Route("api/[controller]")]
    [ApiController]
    public class ApiKeysController : ControllerBase
    {
        private readonly IApiKeyService _service;
        private readonly ILogEventService _logEventService;

        public ApiKeysController(IApiKeyService service, ILogEventService logEventService)
        {
            _service = service;
            _logEventService = logEventService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ApiKeyDTO>>> Get([FromQuery] ApiKeyScope? scope = null)
        {
            return Ok(await _service.GetAsync(scope));
        }

        /// <summary>
        /// Crea una chiave e la restituisce in chiaro <b>una volta sola</b>: sul database ne resta
        /// solo l'impronta.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiKeyCreateResponse>> Create(ApiKeyCreateRequest request)
        {
            try
            {
                return Ok(await _service.CreateAsync(request));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ApiKeysController), nameof(Create), LogEvent.EventsTypes.Error, ex);
                return Problem("Errore nella creazione della chiave.");
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Revoke(int id)
        {
            return await _service.RevokeAsync(id) ? NoContent() : NotFound();
        }
    }
}
