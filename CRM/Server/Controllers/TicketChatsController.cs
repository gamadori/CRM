using System;
using System.Collections.Generic;
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
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CRM.Client.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using MimeKit;
using Microsoft.AspNetCore.Identity;
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
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly ILanguagesService _languageService;
        private readonly TelegramCommandsService _telegramService;
        private readonly UserManager<ApplicationUser> _userManager;

        IHubContext<SignalRHub> _hubContext;

        public TicketChatsController(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService, IHubContext<SignalRHub> hubContext, 
            IEmailSenderPlus emailSenderPlus, ILanguagesService languagesService, TelegramCommandsService telegramService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _hubContext = hubContext;
            _emailSenderPlus = emailSenderPlus;
            _languageService = languagesService;
            _telegramService = telegramService;
            _userManager = userManager;
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

                var listChats = await ticketChats.Select(x=>new TicketChatViewModel() { Id = x.Id, Date = x.Date, Message = x.Message,  IdUser = x.IdUser,
                    TypeMessage = TicketHelper.GetTypeMessage(x, user.IdCompany), Color = x.User.Color, UserName = x.User.UserName}).ToListAsync();

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
                    TypeMessage = TicketHelper.GetTypeMessage( x, user.IdCompany), Color = x.User.Color, UserName = x.User.UserName, IdUser = x.IdUser }).ToListAsync();

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
                var breadcrumb = GetBreadcrumb(ticketChat);

                HttpContext.Response.Headers.Add(ValuesHelper.BreadcrumbHeader, JsonConvert.SerializeObject(breadcrumb));
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
            else
            {
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(PutTicketChat), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
            }

            return NoContent();
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

                    //await _hubContext.Clients.All.SendAsync("ReceiveMessage", ticketChat.IdTicket, ticketChat.Id);
                    

                    await SendAlertNewMessage(ticketChat);

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
                _context.TicketChats.Remove(ticketChat);
                await _context.SaveChangesAsync();
            }
            else
                await _logEventService.RegisterAsync(nameof(TicketChatsController), nameof(DeleteTicketChar), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors); 
            return NoContent();
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

        [HttpGet("HasNewMessage/{id}")]
        public async Task<bool> HasNewMessage(int IdTicket)
        {
            var idUser = await _permitsService.IdUser();

            if (await _context.TicketChatReads.Where(x => x.IdUser == idUser && x.TicketChat.Ticket.Id == IdTicket && x.Displayed == false).AnyAsync())
            {
                return true;
            }
            else
                return false;
        }

        private bool TicketChatExists(int id)
        {
            return _context.TicketChats.Any(e => e.Id == id);
        }


        private List<BreadcrumbModel>? GetBreadcrumb(TicketChat? ticketChat)
        {
            List<BreadcrumbModel> bread = new List<BreadcrumbModel>();

            if (ticketChat == null)
            {
                return null;
            }

            bread.Add(new BreadcrumbModel() { Title = $"Tickets", Url = $"Tickets" });
            bread.Add(new BreadcrumbModel() { Title = $"{ticketChat.Date.ToString("d")}", Url = null });
            
            return bread;
        }

        private async Task<TicketChatViewModel?> GetTicket(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket != null)
                return new TicketChatViewModel() { Date = ticket.DateOpened, Message = ticket.Description, TypeMessage = TypeMessage.TicketMsg, UserName = "Ticket" };
            else
                return null;
        }
        

        private async Task SendUsersNewMessage(TicketChat ticketChat, string userId, string? sender)
        {
            var connections = SignalRHub.Connections;

            var user = await _context.Users.FindAsync(userId);

            if (user != null && connections.ContainsKey(user.UserName))
            {
                
                var id = connections[user.UserName];
                await _hubContext.Clients.Client(id).SendAsync("Notification", new MsgNotify() { Id = ticketChat.Id, Sender = sender });
                //await _hubContext.Clients.All.SendAsync("Notification", new MsgNotify() { Id = ticketChat.Id, Sender = userId });
            }
           

            //await _hubContext.Clients.All.SendAsync("ReceiveMessage", new HubMessage() { IdUserTo = ticketChat.IdUser, IdObject = ticketChat.Id }); // ticketChat.IdUser, ticketChat.IdTicket, ticketChat.Id);
        }

        private async Task SendAlertNewMessage(TicketChat ticketChat)
        {
            var users = await _permitsService.GetUserSendTicketChatAlert(ticketChat.Id);

            var sender = await _context.Users.FindAsync(ticketChat.IdUser);
            if (users != null)
            {
                foreach (var user in users)
                {
                    if (user != sender?.Id)
                    {
                        var chatRead = await _context.TicketChatReads.Where(x => x.IdTicketChat == ticketChat.Id && x.IdUser == user).FirstOrDefaultAsync();

                        if (chatRead == null)
                        {
                            chatRead = new TicketChatRead();
                            chatRead.IdUser = user;
                            chatRead.IdTicketChat = ticketChat.Id;
                            _context.TicketChatReads.Add(chatRead);
                        }
                        chatRead.Displayed = false;

                        await SendUsersNewMessage(ticketChat, user, sender?.NameComplete);
                    }
                    
                }
                await _context.SaveChangesAsync();
                await SendEmail(ticketChat, sender, users);
            }
        }

        private async Task<bool> SendEmail(TicketChat ticketChat, ApplicationUser? sender, List<string> idUsers)
        {
            try
            {
                MimeMessage? msg = null;

                List<ApplicationUser> users = new List<ApplicationUser>();

                


                var ticket = await _context.Tickets.FindAsync(ticketChat.IdTicket);

                if (ticket != null)
                {
                        
                    foreach (var idUser in idUsers)
                    {
                        if (idUser != sender?.Id)
                        {
                            var user = await _userManager.FindByIdAsync(idUser);
                            if (user != null)
                            {
                                users.Add(user);
                            }
                        }
                    }


                    if (users.Any())
                    {
                        var keyValues = new Dictionary<string, string>();
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), DateTime.Now.ToString("g"));

                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Ticket), ticket.Id.ToString());

                        if (sender != null)
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), sender.NameComplete);

                        var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/info/chat/{ticket.Id}/{ticketChat.Id}");

                        if (callbackUrl != null)
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                        List<string> emails = users.Select(x=>x.Email).ToList();

                        if (emails != null && emails.Any())
                            msg = await _emailSenderPlus.SendEmailAsync(emails, EmailsTypes.NoticeNewChatMessage, null, keyValues);

                        if (msg != null)
                        {
                            var phones = users.Where(x => x.PhoneNumber != null).Select(x => x.PhoneNumber).ToList();

                            foreach (var phone in phones)
                            {
                                if (phone != null)
                                    await _telegramService.SendMessage(phone, msg.TextBody);
                            }
                        }
                            return true;
                        
                    }

                }
                return false;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketChat), nameof(SendEmail), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }
    }
}
