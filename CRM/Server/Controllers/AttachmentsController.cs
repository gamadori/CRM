using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using DocumentFormat.OpenXml.Office2021.Excel.RichDataWebImage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TL;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AttachmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly IArchiveService _archiveService;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly TelegramCommandsService _telegramService;
        private readonly Services.ITicketsService _ticketsService;
        private readonly IDocxToPdfConverter _docxToPdfConverter;
        private readonly IAttachmentsService _service;
        public AttachmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermitsService permitsService, IArchiveService archiveService, ILogEventService logEventService, 
            IEmailSenderPlus emailSenderPlus, TelegramCommandsService telegramService, Services.ITicketsService ticketsService, IDocxToPdfConverter docxToPdfConverter, IAttachmentsService service)
        {
            _context = context;
            _userManager = userManager;
            _permitsService = permitsService;
            _archiveService = archiveService;
            _logEventService = logEventService;
            _emailSenderPlus = emailSenderPlus;
            _telegramService = telegramService;
            _ticketsService = ticketsService;
            _docxToPdfConverter = docxToPdfConverter;
            _service = service;
        }


        [HttpGet]
        public async Task<PagingResponse<AttachmentDTO>?> GetPage([FromQuery] AttachmentsFilter? args = null)
        {
            try
            {
                var items = await _service.GetPagingAsync(args) ?? new PagingResponse<AttachmentDTO>();

                foreach (var item in items.Items)
                {
                    item.CanDelete = await _permitsService.CanDeleteAttachment(item.IdUser);
                    item.CanEdit = await _permitsService.CanEditAttachment(item.IdUser);

                }

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(FoldersController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<AttachmentDTO>?> GetItems([FromQuery] AttachmentsFilter? args = null)
        {
            try
            {
                var items = await _service.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<AttachmentDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<AttachmentDTO>();
            }
        }

        // GET: api/SpareParts
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<Attachment>>> GetAttachments([FromQuery] AttachmentsFilter? args = null)
        //{


        //    try
        //    {
        //        if (_context.Attachments == null)
        //        {
        //            return new List<Attachment>();
        //        }
        //        var attachments = _context.Attachments.AsQueryable();
        //        int count;



        //        if (args != null)
        //        {
        //            attachments = attachments.Where(x => x.IdParent == args.IdParant && x.AttchmentType == args.AttchmentType);

        //            if (args.FolderId != null)
        //            {
        //                attachments = attachments.Where(x => x.FolderId == args.FolderId);
        //            }

        //            if (args.Filter != null)
        //            {


        //                attachments = attachments.Where(args.Filter);
        //            }
        //            count = attachments.Count();

        //            if (args.OrderBy != null)
        //                attachments = attachments.OrderBy(args.OrderBy);

        //            if (args.Skip != null && args.Top != null)
        //            {
        //                attachments = attachments.Skip(args.Skip.Value).Take(args.Top.Value);
        //            }

        //        }
        //        else
        //            count = attachments.Count();

        //        var paginationMetadata = new PagingHeaderModel
        //        {
        //            TotalCount = count,
        //            TotalPage = 1,
        //            PageSize = 0
        //        };

        //        foreach (var attachment in attachments)
        //        {
        //            attachment.CanDelete = await _permitsService.CanDeleteAttachment(attachment.IdUser);
        //        }

        //        HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));


        //        return await attachments.ToListAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(GetAttachments), LogEvent.EventsTypes.Error, ex);
        //        return new List<Attachment>();
        //    }
        //}

        [HttpGet("{id}")]
        public async Task<ActionResult<AttachmentDTO>> Get(int id)
        {
            try
            {
                var item = await _service.GetItemAsync(id);
                //List<AttachmentFile> attachmentFiles = new List<AttachmentFile>();
                //var item = await _context.Attachments.Where(x => x.Id == id).FirstOrDefaultAsync();


                //if (item == null)
                //{
                //    return NotFound();
                //}
                //var files = _context.AttachmentFiles.Where(x => x.IdAttachment == id);

                //foreach (var f in files)
                //{
                //    attachmentFiles.Add(new AttachmentFile() { ContentType = f.ContentType, Id = f.Id, Name = f.Name, IdAttachment = f.IdAttachment, Size = f.Size });
                //}
                //item.Files = attachmentFiles.ToArray();
                return item ?? new AttachmentDTO();
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(Get), LogEvent.EventsTypes.Error, ex.Message);
                return new AttachmentDTO();
            }
        }

        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, Attachment item)
        {
            //if (id != item.Id)
            //{
            //    return BadRequest();
            //}

            //_context.Entry(item).State = EntityState.Modified;

            //try
            //{
            //    await _context.SaveChangesAsync();


            //    foreach (var f in item.Files)
            //    {
            //        var ext = Path.GetExtension(f.Name);
            //        _archiveService.SaveAttachments(f.Id, ext, f.Content);
            //    }

            //    await _context.SaveChangesAsync();
            //}
            //catch (DbUpdateConcurrencyException)
            //{
            //    if (!AttachmentExists(id))
            //    {
            //        return NotFound();
            //    }
            //    else
            //    {
            //        throw;
            //    }
            //}

            //return  CreatedAtAction("Get", new { id = item.Id }, new Attachment() { Id = item.Id }); 
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _service.PostAsync(item);

            if (resp == null)
                return Problem("Error saving Attachment");

            return Ok(resp);
        }

        // POST: api/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<AttachmentDTO>>> Post(Attachment item)
        {
            try
            {
                var resp = await _service.PostAsync(item);

                //item.CreatedOn = DateTime.Now.Date;
                //item.IdUser = await _permitsService.IdUser();
                //var resp = await _service.PostAsync(item);

                //_context.Attachments.Add(item);
                //await _context.SaveChangesAsync();

                //foreach (var f in item.Files)
                //{
                //    var ext = Path.GetExtension(f.Name);

                //    _archiveService.SaveAttachments(f.Id, ext, f.Content);
                    
                //}
                //await SendEmail(item);
                //return CreatedAtAction("Get", new { id = item.Id }, new Attachment() { Id = item.Id});
                return Ok(resp);
            }
            catch(Exception ex)
            {

                

                return new APIResponseMessage<AttachmentDTO>()
                {
                    State = false,
                    Message = ex.Message,
                    Data = null
                };

            }
        }

        [HttpGet("CanDelete/{id}")]
        public async Task<ActionResult<bool>> CanDelete(int id)
        {
            return await _service.CanDelete(id);
        }

        [HttpGet("CanEdit/{id}")]
        public async Task<ActionResult<bool>> CanEdit(int id)
        {
            return await _service.CanEdit(id);
        }

        [HttpPost("upload/{idAttachment}")]
        public async Task<IActionResult> UploadFiles(int idAttachment, List<AttachmentFile> files)
        {
            await _service.UploadFiles(idAttachment, files);

            //var attachment = await _context.Attachments.FindAsync(idAttachment);

            //foreach (var f in files)
            //{
            //    f.IdAttachment = idAttachment;
            //    f.ContentType = Path.GetExtension(f.Name);
            //    _context.AttachmentFiles.Add(f);
                
            //    byte[] bytes = Convert.FromBase64String(f.Content);
            //    f.Size = bytes.Length;

            //    await _context.SaveChangesAsync();
            //    _archiveService.SaveAttachments(f.Id, f.ContentType, f.Content);
            //}
            return NoContent();
        }
        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _service.DeleteAsync(id);
            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Attachment");
            }
            else
                return NoContent();
            //var item = await _context.Attachments.FindAsync(id);
            //if (item == null)
            //{
            //    return NotFound();
            //}

            //if (await _permitsService.CanDeleteAttachment(item.IdUser))
            //{
            //    _context.Attachments.Remove(item);
            //    await _context.SaveChangesAsync();
            //}
            //return NoContent();
        }

        [HttpDelete("files/{id}")]
        public async Task<IActionResult> DeleteFiles(int id)
        {
            var resp = await _service.DeleteFiles(id);
            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error On Deleded Attachment File");
            }
            else
                return NoContent();
            //var item = await _context.AttachmentFiles.FindAsync(id);

            //if (item != null)
            //{
            //    _context.AttachmentFiles.Remove(item);
            //}
            //await _context.SaveChangesAsync();

            //return NoContent();
        }

       
        
        [HttpGet("download/{idAttachment}")]
        public async Task<IActionResult> Download(int idAttachment, CancellationToken cancel)
        {
            var resp = await _service.DownloadFiles(idAttachment);
            return File(resp.Bytes, resp.ContentType);

            var attachment = await _context.Attachments.Include(x=>x.Files).Where(x=>x.Id == idAttachment).FirstOrDefaultAsync();
            byte[] bytes;

            if (attachment != null)
            {
                AttachmentResponse header = new AttachmentResponse();
                if (attachment.Files.Count() == 1)
                {
                    var file = attachment.Files.First();
                    header.ContentType = MimeKit.MimeTypes.GetMimeType(file.Name);
                    header.Name = file.Name;
                    bytes = _archiveService.GetAttachment(file.Id, file.Name);

                    
                }
                else
                {
                    header.ContentType = "application/zip";
                    header.Name = attachment.Name + ".zip";

                    using (var memoryStream = new MemoryStream())
                    {
                        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                        {
                            foreach (var f in attachment.Files)
                            {
                                var entry = zipArchive.CreateEntry(f.Name);

                                using (var entryStream = entry.Open())
                                {
                                    entryStream.Write(_archiveService.GetAttachment(f.Id, f.Name));
                                }
                            }
                           
                        }
                        bytes = memoryStream.ToArray();
                    }
                }
                
                HttpContext.Response.Headers.Add(ConstHelper.FileHeader, 
                    JsonConvert.SerializeObject(header));
                
                //var bytes = Convert.FromBase64String(attachment.Content); //, attachment.FileType);
                return File(bytes, header.ContentType);
            }
            else
                return null;
        }

        

        [HttpGet("files/download/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            byte[] bytes;
            var file = await _context.AttachmentFiles.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (file != null)
            {
                AttachmentResponse header = new AttachmentResponse();

                
                header.ContentType = MimeKit.MimeTypes.GetMimeType(file.Name);
                header.Name = file.Name;
                bytes = _archiveService.GetAttachment(file.Id, file.Name);


                HttpContext.Response.Headers.Add(ConstHelper.FileHeader,
                   JsonConvert.SerializeObject(header));


                MemoryStream ms = new MemoryStream(bytes);
                return new FileStreamResult(ms, header.ContentType);
                //var bytes = Convert.FromBase64String(attachment.Content); //, attachment.FileType);
               // return File(bytes, header.ContentType);
            }
            else
                return null;
        }

        [HttpGet("files/{id}")]
        public async Task<IActionResult> GetBytes(int id)
        {
            byte[] bytes;
            var file = await _context.AttachmentFiles.Where(x => x.Id == id).FirstOrDefaultAsync();

            if (file != null)
            {
                AttachmentResponse header = new AttachmentResponse();


                header.ContentType = MimeKit.MimeTypes.GetMimeType(file.Name);
                header.Name = file.Name;

                HttpContext.Response.Headers.Add(ConstHelper.FileHeader,
                   JsonConvert.SerializeObject(header));


                bytes = _archiveService.GetAttachment(file.Id, file.Name);

                return File(
                    bytes,
                    header.ContentType,
                    file.Name);

            }
            else
                return NotFound();
        }

        [HttpGet("files/{id}/pdf")]
        public async Task<IActionResult> GetBytesAsPdf(int id)
        {
            try
            {
                var file = await _context.AttachmentFiles.Where(x => x.Id == id).FirstOrDefaultAsync();

                if (file == null)
                    return NotFound();

                var originalBytes = _archiveService.GetAttachment(file.Id, file.Name);
                var contentType = MimeKit.MimeTypes.GetMimeType(file.Name);

                // Se è già PDF, restituisci direttamente
                if (contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return File(originalBytes, "application/pdf", Path.ChangeExtension(file.Name, ".pdf"));
                }

                // Formati Office supportati per conversione PDF
                var supportedForConversion = new[]
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document", // DOCX
                    "application/msword", // DOC
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", // XLSX
                    "application/vnd.ms-excel", // XLS
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation", // PPTX
                    "application/vnd.ms-powerpoint", // PPT
                    "application/vnd.oasis.opendocument.text", // ODT
                    "application/vnd.oasis.opendocument.spreadsheet", // ODS
                    "application/vnd.oasis.opendocument.presentation" // ODP
                };

                if (supportedForConversion.Contains(contentType, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var pdfBytes = _docxToPdfConverter.ConvertToPdf(originalBytes);
                        return File(pdfBytes, "application/pdf", Path.ChangeExtension(file.Name, ".pdf"));
                    }
                    catch (Exception conversionEx)
                    {
                        // Log dettagliato dell'errore di conversione
                        await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(GetBytesAsPdf), 
                            LogEvent.EventsTypes.Error, 
                            $"Errore conversione {Path.GetExtension(file.Name)}->PDF per file {file.Id} ({file.Name}): {conversionEx.Message}\n{conversionEx.StackTrace}");

                        // Fallback: restituisci il file originale con messaggio di errore nell'header
                        HttpContext.Response.Headers.Add("X-Conversion-Error", "Conversione fallita");
                        return File(originalBytes, contentType, file.Name);
                    }
                }

                // Altrimenti restituisci originale
                return File(originalBytes, contentType, file.Name);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(GetBytesAsPdf), LogEvent.EventsTypes.Error, ex);
                return StatusCode(500, $"Errore durante il recupero del file: {ex.Message}");
            }
        }

        /// <summary>
        /// Rinomina il nome del file mantenendo l'estensione
        /// 
        /// </summary>
        /// <param name="id">ID del file da rinominare</param>
        /// <param name="newName">Nuovo nome del file (senza estensione)</param>
        /// <returns></returns>
        [HttpPut("files/rename/{id}")]
        public async Task<IActionResult> FileRename(int id, [FromBody] string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    return BadRequest("Il nuovo nome non può essere vuoto");
                }

                var file = await _context.AttachmentFiles.Where(x => x.Id == id).FirstOrDefaultAsync();

                if (file == null)
                {
                    return NotFound($"File con ID {id} non trovato");
                }

                var extension = Path.GetExtension(file.Name);
                file.Name = $"{newName}{extension}";

                await _context.SaveChangesAsync();

                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(FileRename), 
                    LogEvent.EventsTypes.Info, $"File {id} rinominato in {file.Name}");

                return Ok(new { success = true, newName = file.Name });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(FileRename), 
                    LogEvent.EventsTypes.Error, ex);
                return StatusCode(500, $"Errore durante la ridenominazione del file: {ex.Message}");
            }
        }
        private bool AttachmentExists(int id)
        {
            return _context.Attachments.Any(e => e.Id == id);
        }


        private async Task<bool> SendEmail(Attachment attachment)
        {
            try
            {
                MimeMessage? msg = null;

                if (attachment.AttchmentType  == AttachmentTypes.Ticket)
                {

                    var user = await _context.Users.FindAsync(attachment.IdUser);

                    var ticket = await _context.Tickets.FindAsync(attachment.IdParent);

                    if (ticket != null)
                    {
                        var addresses = await _ticketsService.GetEmails(attachment.IdParent);

                        
                        if (addresses != null && addresses.Any())
                        {
                            var keyValues = new Dictionary<string, string>();
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g"));

                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Ticket), ticket.Id.ToString());

                            if (user != null)
                                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), user.NameComplete);
                            
                            var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/info/attachment/{ticket.Id}/{attachment.Id}");

                            if (callbackUrl != null)
                                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                            List<string> emails = addresses.Where(x => x.Item1 != null && x.Item1.Any()).Select(x => x.Item1).ToList<string>();

                            if (emails != null && emails.Any())
                                msg = await _emailSenderPlus.SendEmailAsync(emails, EmailsTypes.NewAttachment, null, keyValues);

                            if (msg != null)
                            {
                                var phones = addresses.Where(x => x.Item2 != null && x.Item2.Any()).Select(x => x.Item2).ToList();

                                foreach (var phone in phones)
                                {
                                    if (phone != null)
                                        await _telegramService.SendMessage(phone, msg.TextBody);
                                }
                            }
                            return true;
                        }
                    }

                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(SendEmail), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }


    }
}
