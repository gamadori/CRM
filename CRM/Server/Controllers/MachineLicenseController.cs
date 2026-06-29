using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Endpoint pubblici chiamati dalla macchina industriale.
    /// Non richiedono autenticazione utente: la MachineKey funge da token di identità.
    /// </summary>
    [AllowAnonymous]
    [Route("api/machine-license")]
    [ApiController]
    public class MachineLicenseController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IRsaLicenseService _rsa;
        private readonly ILogger<MachineLicenseController> _logger;

        public MachineLicenseController(ApplicationDbContext db, IRsaLicenseService rsa, ILogger<MachineLicenseController> logger)
        {
            _db = db;
            _rsa = rsa;
            _logger = logger;
        }

        /// <summary>
        /// La macchina chiama questo endpoint al primo avvio (o dopo reset).
        /// Registra la MachineKey associandola alla matricola via SerialNumber.
        /// Se esiste già una licenza configurata, la restituisce subito.
        /// </summary>
        [HttpPost("register")]
        public async Task<ActionResult<MachineRegistrationResponse>> Register([FromBody] MachineRegistrationRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.SerialNumber) || string.IsNullOrWhiteSpace(req.MachineKey))
                return BadRequest(new MachineRegistrationResponse { Success = false, Message = "SerialNumber e MachineKey obbligatori." });

            var article = await _db.Articles.AsNoTracking()
                .FirstOrDefaultAsync(a => a.SerialNumber == req.SerialNumber);

            if (article == null)
            {
                _logger.LogWarning("Registrazione licenza: SerialNumber '{SN}' non trovato.", req.SerialNumber);
                return NotFound(new MachineRegistrationResponse { Success = false, Message = "Matricola non trovata." });
            }

            var lic = await _db.ArticleLicenses
                .Include(l => l.Features).ThenInclude(f => f.FeatureDef)
                .FirstOrDefaultAsync(l => l.IdArticle == article.Id);

            if (lic == null)
            {
                // Nessuna licenza ancora configurata: registra la chiave in attesa
                var pending = new ArticleLicense
                {
                    IdArticle = article.Id,
                    MachineKey = req.MachineKey.Trim(),
                    StartDate = DateTime.UtcNow,
                    IsActive = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ArticleLicenses.Add(pending);
                await _db.SaveChangesAsync();

                _logger.LogInformation("MachineKey registrata per '{SN}' (licenza non ancora configurata).", req.SerialNumber);
                return Ok(new MachineRegistrationResponse
                {
                    Success = true,
                    LicenseAvailable = false,
                    Message = "Chiave registrata. In attesa di configurazione licenza da parte dell'operatore."
                });
            }

            // Aggiorna MachineKey se diversa (es. cambio hardware)
            if (lic.MachineKey != req.MachineKey.Trim())
            {
                _logger.LogInformation("MachineKey aggiornata per '{SN}'.", req.SerialNumber);
                lic.MachineKey = req.MachineKey.Trim();
                lic.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            if (!lic.IsActive)
                return Ok(new MachineRegistrationResponse { Success = true, LicenseAvailable = false, Message = "Licenza disattivata." });

            var payload = _rsa.GenerateLicense(lic, article.SerialNumber);
            return Ok(new MachineRegistrationResponse { Success = true, LicenseAvailable = true, License = payload });
        }

        /// <summary>
        /// La macchina chiama questo endpoint periodicamente (es. ogni ora) per ricevere
        /// la licenza aggiornata. Utile per applicare limitazioni remote senza intervento fisico.
        /// </summary>
        [HttpGet("{machineKey}")]
        public async Task<ActionResult<LicenseFilePayload>> Pull(string machineKey)
        {
            if (string.IsNullOrWhiteSpace(machineKey))
                return BadRequest("MachineKey obbligatoria.");

            var lic = await _db.ArticleLicenses
                .Include(l => l.Article)
                .Include(l => l.Features).ThenInclude(f => f.FeatureDef)
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.MachineKey == machineKey);

            if (lic == null) return NotFound();
            if (!lic.IsActive) return Unauthorized();

            var payload = _rsa.GenerateLicense(lic, lic.Article.SerialNumber);
            return Ok(payload);
        }
    }
}
