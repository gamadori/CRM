using CNM.Authorize;
using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using static CRM.Shared.LogEvent;

namespace CRM.Server.Services
{
    /// <summary>
    /// Implementazione server del servizio TicketFeedback.
    /// Accede direttamente al database.
    /// </summary>
    public class AttachmentsService : IAttachmentsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermitsService _permitsService;
        private readonly IArchiveService _archiveService;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly ITicketsService _ticketsService;
        private readonly TelegramCommandsService _telegramService;
        private readonly ILogEventService _logEventService;
        public AttachmentsService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor,
            IPermitsService permitsService, IArchiveService archiveService, IEmailSenderPlus emailSenderPlus, ITicketsService ticketsService, 
            TelegramCommandsService telegramService, ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
            _permitsService = permitsService;
            _archiveService = archiveService;
            _emailSenderPlus = emailSenderPlus;
            _ticketsService = ticketsService;
            _telegramService = telegramService;
            _logEventService = logEventService;
        }

        public async Task<AttachmentDTO?> GetItemAsync(int id)
        {
            var item = await _context.Attachments.Include(x=>x.Files)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return item != null ? new AttachmentDTO
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IdParent = item.IdParent,
                AttchmentType = item.AttchmentType,
                FolderId = item.FolderId,
                Visibility = item.Visibility,
                CreatedOn = item.CreatedOn,
                IdUser = item.IdUser,
                NameUser = item.User?.NameComplete,
                Folder = item.Folder?.Name,
                Files = item.Files != null ? 
                    item.Files.Select(f => new AttachmentFileDTO 
                    { 
                        Bytes = f.Bytes, 
                        Content = f.Content, 
                        ContentType = f.ContentType, 
                        Id = f.Id, 
                        IdAttachment = f.IdAttachment, 
                        Link = f.Link, 
                        Name = f.Name, 
                        Size = f.Size 
                    }).ToList() : new List<AttachmentFileDTO>()
            } : null;
        }

        public async Task<AttachmentDTO?> GetFirstAsync()
        {
            var item = await _context.Attachments
                .AsNoTracking()
                .FirstOrDefaultAsync();
            return item != null ? new AttachmentDTO()
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IdParent = item.IdParent,
                AttchmentType = item.AttchmentType,
                FolderId = item.FolderId,
                Visibility = item.Visibility,   
                CreatedOn = item.CreatedOn,
                IdUser = item.IdUser,
                NameUser = item.User?.NameComplete,
                Folder = item.Folder?.Name,
                Files = item.Files != null ? item.Files.Select(f => new AttachmentFileDTO
                {
                    Bytes = f.Bytes,
                    Content = f.Content,
                    ContentType = f.ContentType,
                    Id = f.Id,
                    IdAttachment = f.IdAttachment,
                    Link = f.Link,
                    Name = f.Name,
                    Size = f.Size
                }).ToList() : new List<AttachmentFileDTO>()
            } : null;
        }

       

        public async Task<PagingResponse<AttachmentDTO, object>?> GetSummaryAsync(AttachmentsFilter? args)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,


                };
                PagingResponse<AttachmentDTO, object> resp = new PagingResponse<AttachmentDTO, object>()
                {
                    Items = await items.Select(item => new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        Visibility = item.Visibility,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User!.NameComplete,
                        Folder = item.Folder!.Name

                    }).ToListAsync(),
                       
                        
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {

                return null;
            }
        }

        public async Task<PagingResponse<AttachmentDTO>?> GetPagingAsync(AttachmentsFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new();
                }
                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }

                var paginationMetadata = new PagingHeaderModel
                {
                    TotalCount = count,
                    PageSize = args != null ? args.PageSize : 0,
                };

                var itemList = await items.ToListAsync();
                var attachmentDTOs = new List<AttachmentDTO>();
                foreach (var item in itemList)
                {
                    attachmentDTOs.Add(new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        Visibility = item.Visibility,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User?.NameComplete,
                        Folder = item.Folder?.Name,
                        CanDelete = await CanDelete(item.Id),
                        CanEdit = await CanEdit(item.Id),
                    });
                }

                PagingResponse<AttachmentDTO> resp = new PagingResponse<AttachmentDTO>()
                {
                    Items = attachmentDTOs,
                    MetaData = paginationMetadata,
                    Total = "",
                };

                return resp;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<AttachmentDTO>?> GetListAsync(AttachmentsFilter? args = null)
        {
            try
            {
                var items = await FilterItems(args);

                if (items == null)
                {
                    return new List<AttachmentDTO>();
                }

                return await items.Select(item => new AttachmentDTO
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    IdParent = item.IdParent,
                    AttchmentType = item.AttchmentType,
                    FolderId = item.FolderId,
                    Visibility = item.Visibility,
                    CreatedOn = item.CreatedOn,
                    IdUser = item.IdUser,
                    NameUser = item.User!.NameComplete,
                    Folder = item.Folder!.Name
                   
                }).ToListAsync();
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        public async Task<APIResponseMessage<AttachmentDTO>> PostAsync(Attachment  item)
        {
            try
            {

                if (item.Id > 0)
                {
                    _context.Attachments.Update(item);
                }
                else
                {
                    _context.Attachments.Add(item);
                }
                await _context.SaveChangesAsync();

                foreach (var f in item.Files)
                {
                    var ext = Path.GetExtension(f.Name);

                    _archiveService.SaveAttachments(f.Id, ext, f.Content);

                }
                await SendEmail(item);
                return new APIResponseMessage<AttachmentDTO>
                {
                    State = true,
                    Data = new AttachmentDTO
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        IdParent = item.IdParent,
                        AttchmentType = item.AttchmentType,
                        FolderId = item.FolderId,
                        Visibility = item.Visibility,
                        CreatedOn = item.CreatedOn,
                        IdUser = item.IdUser,
                        NameUser = item.User?.NameComplete,
                        Folder = item.Folder?.Name
                    },
                    Message = "Attachment saved successfully",
                    Code = System.Net.HttpStatusCode.OK
                };
            }
            catch (Exception ex)
            {
                return new APIResponseMessage<AttachmentDTO>
                {
                    State = false,
                    Message = "Error saving Attachment",
                    Code = System.Net.HttpStatusCode.InternalServerError
                };
            }
        }

        [AuthorizeRole(ePolicy.SuperUserRole)]
        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.Attachments.FindAsync(id);

            if (item == null)
            {
                return false;
            }
            try
            {

                _context.Attachments.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> CanAdd()
        {
            return (await _permitsService.BelongsToHeadQuarter() || await _permitsService.BelongsToMainCompany());
        }

        public async Task<bool> CanDelete(int id)
        {
            var attachment = await _context.Attachments.FindAsync(id);
            var companyId = await _permitsService.GetIdCompany();

            if (attachment == null || companyId == null)
            {
                return false;
            }

            var companies = await _permitsService.GetIdCompanies();

            return (await _permitsService.BelongsToHeadQuarter() || 
                await _permitsService.BelongsToMainCompany() && companies.Contains(companyId.Value));
        }

        public async Task<bool> CanEdit(int id)
        {
            var attachment = await _context.Attachments.FindAsync(id);
            var companyId = await _permitsService.GetIdCompany();

            if (attachment == null || companyId == null)
            {
                return false;
            }

            var companies = await _permitsService.GetIdCompanies();

            return (await _permitsService.BelongsToHeadQuarter() ||
                await _permitsService.BelongsToMainCompany() && companies.Contains(companyId.Value));
        }


        public async Task<bool> UploadFiles(int idAttachment, List<AttachmentFile> files)
        {
            try
            {
                var attachment = await _context.Attachments.FindAsync(idAttachment);

                foreach (var f in files)
                {
                    f.IdAttachment = idAttachment;
                    f.ContentType = Path.GetExtension(f.Name);
                    _context.AttachmentFiles.Add(f);

                    byte[] bytes = Convert.FromBase64String(f.Content);
                    f.Size = bytes.Length;

                    await _context.SaveChangesAsync();
                    _archiveService.SaveAttachments(f.Id, f.ContentType, f.Content);
                }
                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(UploadFiles), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }
        public async Task<bool> DeleteFiles(int id)
        {
            try
            {
                var item = await _context.AttachmentFiles.FindAsync(id);

                if (item != null)
                {
                    _context.AttachmentFiles.Remove(item);
                }
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AttachmentsController), nameof(DeleteFiles), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }
        
        public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFiles(int idAttachment)
        {
            var attachment = await _context.Attachments.Include(x => x.Files).Where(x => x.Id == idAttachment).FirstOrDefaultAsync();
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

                _httpContextAccessor.HttpContext?.Response.Headers.Add(ConstHelper.FileHeader,
                    JsonConvert.SerializeObject(header));

                return (bytes, header.ContentType, header.Name);
            }
            else
                return (Array.Empty<byte>(), string.Empty, string.Empty);
        }

        public async Task<(byte[] Bytes, string ContentType, string FileName)> DownloadFile(int idFile)
        {
            var file = await _context.AttachmentFiles.Where(x => x.Id == idFile).FirstOrDefaultAsync();

            if (file != null)
            {
                var contentType = MimeKit.MimeTypes.GetMimeType(file.Name);
                var bytes = _archiveService.GetAttachment(file.Id, file.Name);
                return (bytes, contentType, file.Name);
            }

            return (Array.Empty<byte>(), string.Empty, string.Empty);
        }

        private async Task<string?> GetCurrentUserId()
        {
            return await _permitsService.IdUser();
        }


        /// <summary>
        /// Possono essere visualizzati da tutti gli utenti gli attachment pubblici, 
        /// mentre quelli privati solo dagli utenti che hanno accesso alla risorsa a cui sono collegati.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private async Task<IQueryable<Attachment>?> FilterItems(AttachmentsFilter? args = null)
        {
            try
            {
                
                var items = _context.Attachments.AsQueryable();
                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.Name);

                if (!await _permitsService.BelongsToHeadQuarter() && !await _permitsService.BelongsToMainCompany())
                {
                    items = items.Where(x => x.Visibility == AttachmentVisibilities.Public);
                }

                if (args?.IdParant != null)
                    items = items.Where(x => x.IdParent == args.IdParant);
                
                if (args?.AttchmentType != null)
                    items = items.Where(x=>x.AttchmentType == args.AttchmentType);

                if (args?.FolderId != null)
                    items = items.Where(x => x.FolderId == args.FolderId);

                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                return items;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<bool> SendEmail(Attachment attachment)
        {
            try
            {
                MimeMessage? msg = null;

                if (attachment.AttchmentType == AttachmentTypes.Ticket)
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

                            var callbackUrl = _httpContextAccessor?.HttpContext?.AbsoluteUrl($"/Tickets/info/attachment/{ticket.Id}/{attachment.Id}");

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
                                    if (phone != null && msg.TextBody != null)
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
