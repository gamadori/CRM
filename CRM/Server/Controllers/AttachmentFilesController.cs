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
        /// Upload di un file
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

                // Verifico che il ticket di intervento non abbia già un file allegato (opzionale, dipende dai requisiti)
                var attachment = await _context.Attachments.Where(a => a.IdParent == IdTicketIntervention && a.AttchmentType == AttachmentTypes.ExpenseReceipts).FirstOrDefaultAsync();

                if (attachment == null)
                {                     // Creo un nuovo attachment se non esiste
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


                if (file == null || file.Length == 0)
                    return BadRequest("Nessun file fornito");

                if (file.Length > 10 * 1024 * 1024)
                    return BadRequest("File troppo grande (massimo 10 MB)");

                // Leggi il contenuto del file
                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                // Crea record AttachmentFile
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
            catch (Exception ex)
            {
                return StatusCode(500, "Errore durante l'upload del file");
            }
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
