using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using CRM.Server.Services;
using CRM.Server.Helpers;
using CRM.Shared.Helper;
using Microsoft.Extensions.Primitives;
using CRM.Client.Helpers;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketChatsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ITicketChatNotificationService _ticketChatNotificationService;
        private readonly IArchiveService _archiveService;

        public TicketChatsController(
            ApplicationDbContext context,
            IPermitsService permitsService,
            ILogEventService logEventService,
            ITicketChatNotificationService ticketChatNotificationService,
            IArchiveService archiveService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _ticketChatNotificationService = ticketChatNotificationService;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Attachments;
        }


      

        // GET: api/Companies
        [HttpGet]
        public async Task<ObjectView<TicketChatViewModel, string>?> GetTicketChats([FromQuery] TicketChatFilterModel? args = null)
        {
            try
            {
                int totalPage = 1;
                int? idCompany;
                bool addTicketMsg = false;
                
                var  ticketChats = _context.TicketChats.AsQueryable();

                
                ticketChats = ticketChats.OrderBy(x => x.Date);

                if (!await _permitsService.CanAccessOtherCompany())
                {
                    idCompany = await _permitsService.GetIdCompany();
                    ticketChats = ticketChats.Where(x => x.Ticket.IdCompany == idCompany);
                }

                if (args?.IdTicket != null)
                {
                    ticketChats = ticketChats.Where(x => x.IdTicket == args.IdTicket);
                }

                if (args?.IdUser != null && args.IdUser.Length > 0)
                {
                    ticketChats = ticketChats.Where(x => x.IdUser == args.IdUser);
                }

                int count = ticketChats.Count() + 1;

                if (args?.Skip != null && args.Top != null)
                {
                    int top = args.Top.Value;

                    addTicketMsg = (args.Skip == 0); // (args.Skip + args.Top >= count - 1);

                    if (addTicketMsg)
                        top = top >= 1 ? top - 1: top;
                    

                    ticketChats = ticketChats.Skip(args.Skip.Value).Take(top);
                    
                }
                
                // var list = await companies.ToListAsync();
                var user = await _permitsService.GetUser();

                var listChats = await ticketChats.Select(x => new TicketChatViewModel
                {
                    Id = x.Id, Date = x.Date, Message = x.Deleted ? null : x.Message, IdUser = x.IdUser,
                    TypeMessage = TicketHelper.GetTypeMessage(x, user.IdCompany),
                    Color = x.User != null ? x.User.Color : null,
                    UserName = x.User != null ? x.User.UserName : x.ExternalSender,
                    Deleted = x.Deleted,
                    CanDelete = !x.Deleted && x.IdUser == user.Id,
                    AttachmentFileId = x.Deleted ? null : x.IdAttachmentFile,
                    AttachmentId = !x.Deleted && x.AttachmentFile != null ? (int?)x.AttachmentFile.IdAttachment : null,
                    AttachmentFileName = !x.Deleted && x.AttachmentFile != null ? x.AttachmentFile.Name : null,
                    AttachmentContentType = !x.Deleted && x.AttachmentFile != null ? x.AttachmentFile.ContentType : null,
                }).ToListAsync();

                if (args != null  && args.IdTicket != null)
                {
                    var item = await GetTicket(args.IdTicket.Value);
                    if (item != null)
                    {
                        listChats.Insert(0, item);
                    }
                }

                if (listChats.Any())
                    listChats[listChats.Count - 1].Last = true;
                //listChats.Add(new TicketChatViewModel() { TypeMessage = TypeMessage.End });

                var paginationMetadata = new
                {
                    totalCount = count,


                };
                
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                return new ObjectView<TicketChatViewModel, string>() { Items = listChats, Total = listChats.Count().ToString() };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(GetTicketChats), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("LastPage")]
        public async Task<IEnumerable<TicketChatViewModel>?> GetLastTicketChats([FromQuery] TicketChatFilterModel? args = null)
        {
            try
            {

                int totalPage = 1;
                int? idCompany;


                var ticketChats = _context.TicketChats.AsQueryable();

                

                ticketChats = ticketChats.OrderByDescending(x => x.Date);

                if (!await _permitsService.CanAccessOtherCompany())
                {
                    idCompany = await _permitsService.GetIdCompany();
                    ticketChats = ticketChats.Where(x => x.Ticket.IdCompany == idCompany);
                }


                if (args?.IdTicket != null)
                {
                    ticketChats = ticketChats.Where(x => x.IdTicket == args.IdTicket);
                }
                if (args?.IdUser != null && args.IdUser.Length > 0)
                {
                    ticketChats = ticketChats.Where(x => x.IdUser == args.IdUser);
                }



                int count = ticketChats.Count();

                if (args == null)
                    args = new TicketChatFilterModel();

               
                args.Skip = count - 10;
                args.Top = 10;

                if (args?.Skip != null && args.Top != null)
                {
                    ticketChats = ticketChats.Skip(args.Skip.Value).Take(args.Top.Value);
                }
                else if (args != null && args.PageSize > 0)
                {
                    ticketChats = ticketChats.Skip((args.PageNumber - 1) * args.PageSize).Take(args.PageSize);
                    totalPage = (int)Math.Ceiling(count / (double)args.PageSize);
                }
                else
                {
                    totalPage = 1;

                }
                bool nextPage = args?.PageNumber < totalPage;
                bool previousPage = args?.PageNumber > 1;

                var paginationMetadata = new
                {
                    totalCount = count,
                    pageSize = args != null ? args.PageSize : 0,
                    currentPage = args != null ? args.PageNumber : 0,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                // var list = await companies.ToListAsync();
                var user = await _permitsService.GetUser();

                var listChats = await ticketChats.Select(x => new TicketChatViewModel() { Id = x.Id, Date = x.Date, Message = x.Message,
                    TypeMessage = TicketHelper.GetTypeMessage( x, user.IdCompany),
                    Color = x.User != null ? x.User.Color : null,
                    UserName = x.User != null ? x.User.UserName : x.ExternalSender,
                    IdUser = x.IdUser }).ToListAsync();

                return listChats;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(GetTicketChats), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


        // GET: api/Companies/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketChat>> GetTicketChat(int id)
        {
            var ticketChat = await _context.TicketChats.FindAsync(id);

            if (ticketChat == null)
            {
                return NotFound();
            }
            else if (await _permitsService.CanGetTicket(ticketChat.IdTicket))
            {
                return ticketChat;
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(GetTicketChat), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return BadRequest();
            }
        }
       

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketChat(int id, TicketChat ticketChat)
        {
            if (id != ticketChat.Id)
            {
                return BadRequest();
            }
            else if (!await _permitsService.CanEditTicketChat(id))
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(PutTicketChat), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return Forbid();
            }
            else
            {
                _context.Entry(ticketChat).State = EntityState.Modified;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketChatExists(id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return NoContent();
        }

        [HttpPost("{idTicket}/upload")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<ChatFileUploadResult>> UploadChatFile(int idTicket, IFormFile file)
        {
            if (!await _permitsService.CanInsertTicketChat(idTicket))
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest("Nessun file fornito.");

            if (file.Length > 20 * 1024 * 1024)
                return BadRequest("File troppo grande (massimo 20 MB).");

            try
            {
                var idUser = await _permitsService.IdUser();

                // Crea un Attachment individuale per questo file (la Description verrà
                // aggiornata con il testo del messaggio quando il messaggio viene inviato)
                var attachment = new Attachment
                {
                    IdParent = idTicket,
                    AttchmentType = AttachmentTypes.Ticket,
                    Name = Path.GetFileName(file.FileName),
                    Description = "",
                    CreatedOn = DateTime.UtcNow,
                    IdUser = idUser,
                    Visibility = AttachmentVisibilities.Private
                };
                _context.Attachments.Add(attachment);
                await _context.SaveChangesAsync();

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
                    ContentType = file.ContentType,
                    IdAttachment = attachment.Id
                };
                _context.AttachmentFiles.Add(attachmentFile);
                await _context.SaveChangesAsync();

                var ext = Path.GetExtension(attachmentFile.Name);
                _archiveService.SaveAttachments(attachmentFile.Id, ext, fileBytes);

                return Ok(new ChatFileUploadResult
                {
                    Id = attachmentFile.Id,
                    AttachmentId = attachment.Id,
                    Name = attachmentFile.Name,
                    ContentType = attachmentFile.ContentType
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(UploadChatFile), LogEvent.EventsTypes.Error, ex);
                return StatusCode(500, "Errore durante l'upload del file.");
            }
        }

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Company>> PostTicketChat(TicketChat ticketChat)
        {
            try
            {
                if (await _permitsService.CanInsertTicketChat(ticketChat.IdTicket))
                {
                    ticketChat.Date = DateTime.Now;
                    ticketChat.IdUser = await _permitsService.IdUser();

                    _context.TicketChats.Add(ticketChat);
                    await _context.SaveChangesAsync();

                    // Aggiorna la descrizione dell'allegato con il testo del messaggio
                    if (ticketChat.IdAttachmentFile.HasValue && !string.IsNullOrWhiteSpace(ticketChat.Message))
                    {
                        var attachmentFile = await _context.AttachmentFiles
                            .FindAsync(ticketChat.IdAttachmentFile.Value);
                        if (attachmentFile != null)
                        {
                            var attachment = await _context.Attachments.FindAsync(attachmentFile.IdAttachment);
                            if (attachment != null)
                            {
                                attachment.Description = ticketChat.Message.Length > 100
                                    ? ticketChat.Message[..97] + "..."
                                    : ticketChat.Message;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }

                    await _ticketChatNotificationService.NotifyNewMessageAsync(ticketChat.Id);

                    return CreatedAtAction("GetTicketChat", new { id = ticketChat.Id }, ticketChat);
                }
                else
                {
                    await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(PostTicketChat), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(PostTicketChat), LogEvent.EventsTypes.Error, ex);
                return BadRequest();
            }
        }

        // DELETE: api/Companies/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketChar(int id)
        {
            var ticketChat = await _context.TicketChats.FindAsync(id);
            if (ticketChat == null)
            {
                return NotFound();
            }

            if (await _permitsService.CanDeleteObject(ticketChat.IdUser))
            {
                // Eliminazione stile WhatsApp: la riga resta ma testo e allegato
                // vengono rimossi e il messaggio viene marcato come eliminato.
                await DeleteChatAttachment(ticketChat);

                ticketChat.Message = null;
                ticketChat.Deleted = true;
                await _context.SaveChangesAsync();
            }
            else
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(DeleteTicketChar), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
            return NoContent();
        }

        /// <summary>
        /// Rimuove l'eventuale allegato collegato al messaggio: file fisico,
        /// AttachmentFile e Attachment. Sgancia il riferimento sul TicketChat.
        /// </summary>
        private async Task DeleteChatAttachment(TicketChat ticketChat)
        {
            if (!ticketChat.IdAttachmentFile.HasValue)
                return;

            var attachmentFile = await _context.AttachmentFiles.FindAsync(ticketChat.IdAttachmentFile.Value);

            // Sgancia il riferimento prima di rimuovere le entità collegate.
            ticketChat.IdAttachmentFile = null;

            if (attachmentFile != null)
            {
                try
                {
                    _archiveService.Delete(attachmentFile.Id, attachmentFile.Name);
                }
                catch (Exception ex)
                {
                    await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(DeleteChatAttachment), LogEvent.EventsTypes.Error, ex);
                }

                var attachment = await _context.Attachments.FindAsync(attachmentFile.IdAttachment);

                _context.AttachmentFiles.Remove(attachmentFile);
                if (attachment != null)
                    _context.Attachments.Remove(attachment);
            }
        }

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("MessageRead/{id}")]
        public async Task<IActionResult> MessageRead(int id, TicketChatViewModel item)
        {
            try
            {
                if (id != item.Id)
                {
                    return BadRequest();
                }
                else
                {
                    var ticketChat = await _context.TicketChats.FindAsync(id);

                    if (ticketChat == null)
                        return BadRequest();

                    if (await _permitsService.CanGetTicket(ticketChat.IdTicket))
                    {
                        var idUser = await _permitsService.IdUser();

                        var ticketToRead = await _context.TicketChatReads.Where(x => x.IdTicketChat == id && x.IdUser == idUser && x.Displayed == false).FirstOrDefaultAsync();

                        if (ticketToRead != null)
                        {
                            ticketToRead.DateRead = DateTime.Now;
                            ticketToRead.Displayed = true;
                            await _context.SaveChangesAsync();
                        }

                    }


                }

                return NoContent();
            }
            catch (Exception ex)
            {

                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(MessageRead), LogEvent.EventsTypes.Error, ex);
                return NoContent();
            }
        }

        [HttpPut("TicketRead/{idTicket}")]
        public async Task<IActionResult> TicketRead(int idTicket)
        {
            try
            {
                if (!await _permitsService.CanGetTicket(idTicket))
                {
                    await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(TicketRead), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                    return Forbid();
                }

                var idUser = await _permitsService.IdUser();
                var unreadMessages = await _context.TicketChatReads
                    .Where(x => x.IdUser == idUser
                        && x.Displayed == false
                        && x.TicketChat.IdTicket == idTicket)
                    .ToListAsync();

                if (unreadMessages.Count == 0)
                    return NoContent();

                var readDate = DateTime.Now;
                foreach (var unreadMessage in unreadMessages)
                {
                    unreadMessage.DateRead = readDate;
                    unreadMessage.Displayed = true;
                }

                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(TicketRead), LogEvent.EventsTypes.Error, ex);
                return NoContent();
            }
        }

        [HttpGet("HasNewMessage/{id}")]
        public async Task<bool> HasNewMessage(int id)
        {
            var idUser = await _permitsService.IdUser();

            return await _context.TicketChatReads
                .AnyAsync(x => x.IdUser == idUser && x.TicketChat.Ticket.Id == id && x.Displayed == false);
        }

        private bool TicketChatExists(int id)
        {
            return _context.TicketChats.Any(e => e.Id == id);
        }

        private async Task<TicketChatViewModel?> GetTicket(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
                return new TicketChatViewModel() { Date = ticket.DateOpened, Message = ticket.Description, TypeMessage = TypeMessage.TicketMsg, UserName = "Ticket" };
            else
                return null;
        }
    }
}
