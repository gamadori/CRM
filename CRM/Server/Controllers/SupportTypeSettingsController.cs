using CNM.Authorize;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Che firma serve per ogni tipo di intervento.
    /// <para>
    /// La lettura restituisce sempre tutti i tipi, anche quelli senza riga in tabella, cosi' la
    /// pagina delle impostazioni mostra l'elenco completo e non solo cio' che qualcuno ha gia'
    /// toccato. Un tipo senza riga vale "nessuna firma".
    /// </para>
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SupportTypeSettingsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SupportTypeSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<SupportTypeSetting>>> Get()
        {
            var saved = await _context.SupportTypeSettings.AsNoTracking().ToListAsync();

            return Enum.GetValues<TypesSupport>()
                .Select(tipo => saved.FirstOrDefault(x => x.SupportType == (int)tipo)
                                ?? new SupportTypeSetting { SupportType = (int)tipo })
                .OrderBy(x => x.SupportType)
                .ToList();
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] List<SupportTypeSetting> settings)
        {
            if (settings == null)
                return BadRequest();

            var valid = Enum.GetValues<TypesSupport>().Select(x => (int)x).ToHashSet();
            var saved = await _context.SupportTypeSettings.ToListAsync();

            foreach (var item in settings)
            {
                // Un tipo che non esiste nell'enum non si salva: sarebbe una riga che nessun
                // intervento potra' mai leggere.
                if (!valid.Contains(item.SupportType))
                    continue;

                var row = saved.FirstOrDefault(x => x.SupportType == item.SupportType);

                if (row == null)
                    _context.SupportTypeSettings.Add(new SupportTypeSetting
                    {
                        SupportType = item.SupportType,
                        SignatureRequirement = item.SignatureRequirement
                    });
                else
                    row.SignatureRequirement = item.SignatureRequirement;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
