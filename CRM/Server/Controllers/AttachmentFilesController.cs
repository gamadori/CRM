using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Controllers
{
    /// <summary>
    /// Controller per la gestione dei file allegati
    /// </summary>
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentFilesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        private readonly IPermitsService _permitsService;
        public AttachmentFilesController(
            ApplicationDbContext context,
            IArchiveService archiveService, IPermitsService permitsService)
        {
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Attachments;
            _permitsService = permitsService;
        }

        /// <summary>
        /// Upload di uno scontrino legato a un intervento: i file finiscono nel contenitore
        /// dell'intervento, condiviso da tutte le note spese di quell'intervento.
        /// </summary>
        [HttpPost("{IdTicketIntervention}/upload")]
        public async Task<ActionResult> Upload(int IdTicketIntervention, IFormFile file)
        {
            try
            {
                // Verifico che il ticket di intervento esista
                var ticket = await _context.TicketsInterventions.FindAsync(IdTicketIntervention);

                if (ticket == null)
                    return NotFound("Ticket di intervento non trovato");

                var attachment = await _context.Attachments
                    .Where(a => a.IdParent == IdTicketIntervention && a.AttchmentType == AttachmentTypes.ExpenseReceipts)
                    .FirstOrDefaultAsync();

                if (attachment == null)
                {
                    attachment = new Attachment
                    {
                        IdParent = IdTicketIntervention,
                        AttchmentType = AttachmentTypes.ExpenseReceipts,
                        Name = $"Allegati Ticket {IdTicketIntervention}",
                        CreatedOn = DateTime.UtcNow,
                        IdUser = await _permitsService.IdUser(),
                        CanEdit = true,
                        CanDelete = true,
                        Visibility = AttachmentVisibilities.Private
                    };
                    _context.Attachments.Add(attachment);
                    await _context.SaveChangesAsync();
                }

                return await SaveUploadedFileAsync(attachment, file);
            }
            catch (Exception)
            {
                return StatusCode(500, "Errore durante l'upload del file");
            }
        }

        /// <summary>
        /// Upload di uno scontrino SENZA intervento, per le spese che un intervento non ce l'hanno:
        /// trasferta pre-vendita, visita di cortesia, corso, costo generale. Di uno scontrino del
        /// taxi in fiera la foto serve comunque, e prima non c'era modo di caricarla.
        /// <para>
        /// Il contenitore viene creato per singolo caricamento e intestato a chi carica
        /// (<c>IdUser</c> + visibilita' privata): un unico contenitore condiviso avrebbe messo gli
        /// scontrini di tutti sotto il primo che ne ha caricato uno.
        /// </para>
        /// <para>
        /// <c>IdParent = 0</c> significa "nessuna entita' padre": il campo e' un riferimento
        /// polimorfico qualificato da <c>AttchmentType</c>, senza chiave esterna, e le identita'
        /// partono da 1, quindi lo zero non puo' collidere con un intervento reale. La via
        /// alternativa - rendere <c>IdParent</c> nullable - tocca 71 punti in 33 file, inclusa la
        /// gerarchia dei prodotti che usa lo stesso nome: raggio d'azione sproporzionato.
        /// </para>
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult> UploadWithoutParent(IFormFile file)
        {
            try
            {
                var idUser = await _permitsService.IdUser();

                var attachment = new Attachment
                {
                    IdParent = 0,
                    AttchmentType = AttachmentTypes.ExpenseReceipts,
                    Name = $"Nota spese {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
                    CreatedOn = DateTime.UtcNow,
                    IdUser = idUser,
                    CanEdit = true,
                    CanDelete = true,
                    Visibility = AttachmentVisibilities.Private
                };

                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();

                return await SaveUploadedFileAsync(attachment, file);
            }
            catch (Exception)
            {
                return StatusCode(500, "Errore durante l'upload del file");
            }
        }

        /// <summary>
        /// Validazione, persistenza del record e scrittura su disco: la parte identica fra
        /// l'upload con e senza intervento.
        /// </summary>
        private async Task<ActionResult> SaveUploadedFileAsync(Attachment attachment, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nessun file fornito");

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("File troppo grande (massimo 10 MB)");

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            var attachmentFile = new AttachmentFile
            {
                Name = Path.GetFileName(file.FileName),
                Size = file.Length,
                IdAttachment = attachment.Id,
                ContentType = file.ContentType
            };

            _context.AttachmentFiles.Add(attachmentFile);
            await _context.SaveChangesAsync();

            // Salva fisicamente il file
            var ext = Path.GetExtension(attachmentFile.Name);
            _archiveService.SaveAttachments(attachmentFile.Id, ext, fileBytes);

            return Ok(new
            {
                attachmentFile.Id,
                attachmentFile.Name,
                attachmentFile.Size,
                attachmentFile.ContentType
            });
        }

        /// <summary>
        /// Download di un file
        /// </summary>
        [HttpGet("{id}/download")]
        public async Task<ActionResult> Download(int id)
        {
            try
            {
                var attachmentFile = await _context.AttachmentFiles.FindAsync(id);
                if (attachmentFile == null)
                    return NotFound();

                var ext = Path.GetExtension(attachmentFile.Name);
                var fileBytes = _archiveService.GetAttachment(attachmentFile.Id, ext);
                if (fileBytes == null || fileBytes.Length == 0)
                    return NotFound("File non trovato nell'archivio");

                return File(fileBytes, attachmentFile.ContentType ?? "application/octet-stream", attachmentFile.Name);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Errore durante il download del file");
            }
        }

        /// <summary>
        /// Elimina un file
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            try
            {
                var attachmentFile = await _context.AttachmentFiles.FindAsync(id);
                if (attachmentFile == null)
                    return NotFound();

                // Verifica se è usato da ExpenseReceipts
                var usedByReceipts = await _context.ExpenseReceipts.AnyAsync(er => er.AttachmentFileId == id);
                if (usedByReceipts)
                    return BadRequest("Impossibile eliminare: file in uso da una o più note spese");

                // Elimina fisicamente il file (opzionale - commentato per sicurezza)
                // var ext = Path.GetExtension(attachmentFile.Name);
                // File.Delete(_archiveService.GetPath(attachmentFile.Id, ext));

                // Elimina dal database
                _context.AttachmentFiles.Remove(attachmentFile);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Errore durante l'eliminazione del file");
            }
        }
    }
}
