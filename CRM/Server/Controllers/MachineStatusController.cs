using CRM.Server.Services;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Quello che il CRM mostra di una macchina: componenti, versioni e giornate di lavoro.
    /// <para>
    /// Qui si legge soltanto. A scrivere e' la macchina, che passa da
    /// <see cref="MachineParametersController"/> con la sua chiave.
    /// </para>
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MachineStatusController : ControllerBase
    {
        private readonly IMachineStatusService _service;
        private readonly IPermitsService _permits;

        public MachineStatusController(IMachineStatusService service, IPermitsService permits)
        {
            _service = service;
            _permits = permits;
        }

        [HttpGet("{idArticle:int}")]
        public async Task<ActionResult<MachineStatusOverviewDTO>> Get(int idArticle, [FromQuery] int days = 30)
        {
            // Stesso perimetro dei backup: si vede la macchina solo se si vede la sua azienda.
            if (!await _permits.ArticleCanAccess(idArticle))
                return Forbid();

            return Ok(await _service.GetOverviewAsync(idArticle, days, HttpContext.RequestAborted));
        }
    }
}
