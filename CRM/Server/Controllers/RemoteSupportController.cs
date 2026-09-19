using System;
using System.Linq;
using System.Threading.Tasks;
using CNM.Authorize;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Assistenza remota delle macchine BM: emissione del codice di abbinamento,
    /// stato del pannello, apertura della sessione Guacamole e revoca. Il perimetro
    /// è lo stesso dei backup: si agisce su una macchina solo se si vede la sua azienda.
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RemoteSupportController : ControllerBase
    {
        private readonly IRemoteSupportService _service;
        private readonly IPermitsService _permits;

        public RemoteSupportController(IRemoteSupportService service, IPermitsService permits)
        {
            _service = service;
            _permits = permits;
        }

        /// <summary>Stato del pannello. Visibile a chiunque veda la macchina.</summary>
        [HttpGet("status/{idArticle:int}")]
        public async Task<ActionResult<RemoteSupportStatusDTO>> Status(int idArticle)
        {
            if (!await _permits.ArticleCanAccess(idArticle))
                return Forbid();

            return Ok(await _service.StatusAsync(idArticle, HttpContext.RequestAborted));
        }

        /// <summary>Genera il codice monouso da inserire sul pannello.</summary>
        [HttpPost("code/{idArticle:int}")]
        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<ActionResult<RemoteSupportCodeDTO>> IssueCode(int idArticle)
        {
            if (!await _permits.ArticleCanAccess(idArticle))
                return Forbid();

            try
            {
                return Ok(await _service.IssueCodeAsync(idArticle, Actor, HttpContext.RequestAborted));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Apre la sessione di assistenza su una o più macchine (un solo accesso Guacamole).</summary>
        [HttpPost("start")]
        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<ActionResult<RemoteSupportStartResultDTO>> Start([FromBody] RemoteSupportStartRequest request)
        {
            var ids = request?.ArticleIds?.Distinct().ToList() ?? new();
            if (ids.Count == 0)
                return BadRequest("Nessuna macchina indicata.");

            foreach (var id in ids)
            {
                if (!await _permits.ArticleCanAccess(id))
                    return Forbid();
            }

            try
            {
                return Ok(await _service.StartAsync(ids, Actor, HttpContext.RequestAborted));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Revoca l'abbinamento: interrompe il tunnel del pannello. Solo Admin/SuperUser.</summary>
        [HttpPost("revoke/{idArticle:int}")]
        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<IActionResult> Revoke(int idArticle)
        {
            if (!await _permits.ArticleCanAccess(idArticle))
                return Forbid();

            try
            {
                await _service.RevokeAsync(idArticle, Actor, HttpContext.RequestAborted);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string Actor => User.Identity?.Name ?? "portal";
    }
}
