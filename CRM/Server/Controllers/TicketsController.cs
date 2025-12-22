using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Identity;
using CRM.Server.Extensions;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq.Dynamic.Core;
using Syncfusion.Blazor.Data;
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.IdentityModel.Tokens;
using CRM.Client.Pages.DashBoard;
using Microsoft.Data.SqlClient;
using Humanizer;
using CRM.Client.Services;
using System.Composition;
using SelectPdf;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permits;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSenderPlus;
        private readonly ILanguagesService _languageService;
        private readonly TelegramCommandsService _TelegramService;
        private readonly IArchiveService _archiveService;
        private readonly OpenAIEmbeddingService _embeddingService;
        public TicketsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService, IEmailSenderPlus emailSenderPlus, ILanguagesService languageService, 
            TelegramCommandsService telegram, IArchiveService archiveService, OpenAIEmbeddingService embeddingService)
        {
            _context = context;
            _userManager = userManager;
            _permits = permitsService;
            _logEventService = logEventService;
            _emailSenderPlus = emailSenderPlus;
            _languageService = languageService;
            _TelegramService = telegram;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Temp;
            _embeddingService = embeddingService;
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<TicketModel>>> SearchTickets([FromQuery] TicketFilter args)
        {
            try
            {
                List<object> parms = new List<object>();

                if (args.Top == null)
                    args.Top = 10;

                if (args.Skip == null)
                    args.Skip = 0;

                var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, @Search, LANGUAGE N'Italian') AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";

                 //var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN CONTAINSTABLE(Tickets, Description, '@Search') AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";

                var pSearch = new SqlParameter("@Search", args.Search);

                parms.Add(pSearch);

                string where = string.Empty;

                if (args.IdCompany != null || args.IdProduct != null || args.IdArticle != null)
                {
                    where = "where ";

                    if (args.IdCompany != null)
                    {
                        where += $" FT_TBL.IdCompany = @IdCompany";
                        parms.Add(new SqlParameter("@IdCompany", args.IdCompany));
                    }
                    if (args.IdProduct != null)
                    {
                        where += " FT_TBL.IdProduct = @IdProduct";
                        parms.Add(new SqlParameter("@IdProduct", args.IdProduct));
                    }
                    if (args.IdArticle != null)
                    {
                        where += " FT_TBL.IdArticke = @IdArticle";
                        parms.Add(new SqlParameter("@IdArticle", args.IdArticle));
                    }
                }

                var orderby = $"ORDER BY KEY_TBL.RANK DESC OFFSET {args.Skip} ROWS FETCH NEXT {args.Top} ROWS ONLY";
                
                //FormattableString sql = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, {args.Search}, LANGUAGE N'Italian', 2) AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY] ORDER BY KEY_TBL.RANK DESC OFFSET {args.Skip} ROWS FETCH NEXT {args.Top} ROWS ONLY";
                var sql = select + where;

                var total = await _context.Tickets.FromSqlRaw(sql, parms.ToArray()).CountAsync();

                var paginationMetadata = new
                {
                    totalCount = total,
                    
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                sql += orderby;


                List <TicketModel> tickets = await _context.Tickets.FromSqlRaw(sql, parms.ToArray()).Select(x => new TicketModel()
                {
                    Description = x.Description,
                    Id = x.Id,
                    Rank = x.RANK
                }).ToListAsync();

                return tickets;
            }
            catch (Exception ex)
            {
                return new List<TicketModel>();
            }
        }

        // GET: api/Tickets
        [HttpGet]
        public async Task<ActionResult<ObjectView<TicketModel, string>>> GetTicket([FromQuery] TicketFilter args)
        {
            try
            {
                string idUser = await _permits.IdUser();

                DateTime dateTo;

                IQueryable<Ticket> tickets = _context.Tickets;

              
               
                if (args.OrderBy != null)
                {
                    tickets = tickets.OrderBy(args.OrderBy);
                }
                else
                {
                    tickets = tickets.OrderByDescending(x => x.Date);
                }

                if (!await _permits.CanAccessOtherCompany())
                {
                    var idCompany = await _permits.GetIdCompany();
                    tickets = tickets.Where(x => x.IdCompany == idCompany);
                }
                if (args.DateFrom != null)
                {
                    tickets = tickets.Where(x => x.Date >= args.DateFrom || x.DateEnd >= args.DateFrom);
                }

                if (args.DateTo != null)
                {
                    dateTo = args.DateTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.Date < dateTo || x.DateEnd < dateTo);
                }

                if (args.DateClosedFrom != null)
                {
                    tickets = tickets.Where(x => x.DateClosed >= args.DateClosedFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.DateExpiredFrom != null)
                {
                    tickets = tickets.Where(x => x.DateExpired >= args.DateExpiredFrom);
                }

                if (args.DateClosedTo != null)
                {
                    dateTo = args.DateClosedTo.Value.AddDays(1);
                    tickets = tickets.Where(x => x.DateClosed < dateTo);
                }

                if (args.IdCompany != null)
                {
                    tickets = tickets.Where(x => x.IdCompany == args.IdCompany);
                }

                if (args.IdArticle != null)
                    tickets = tickets.Where(x => x.IdArticle == args.IdArticle);

                if (args.IdUserOpened != null)
                {
                    tickets = tickets.Where(x => x.IdUserOpened == args.IdUserOpened);
                }

                if (args.IdUserAssigned != null && args.TypeSearch != (int)TicketTypeSearch.NotAssigned && args.TypeSearch != (int)TicketTypeSearch.NewMessage)
                {
                    if (args.ViewNotAssigned)
                        tickets = tickets.Where(x => (x.IdUserAssigned == args.IdUserAssigned || x.IdUserAssigned == null));
                    else
                        tickets = tickets.Where(x => x.IdUserAssigned == args.IdUserAssigned);
                }

                if (args.IdProject != null)
                {
                    tickets = tickets.Where(x => x.IdProject == args.IdProject);
                }

               

                tickets = GetTicketFiltered(tickets, (TicketTypeSearch)args.TypeSearch, idUser);

                

                if (args.Filter != null && args.Filter.Length > 0)
                {
                    tickets = tickets.Where(args.Filter);
                }

                

                var totalWork = _context.TicketsInterventions.Where(x => tickets.Contains(x.Ticket)).Sum(y=>y.Minute);

                int count = tickets != null ? tickets.Count(): 0;

                


                if (tickets != null && args?.Skip != null && args.Top != null)
                {
                    tickets = tickets.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var ticketModel = tickets.Select(x => new TicketModel()
                {
                    Id = x.Id,
                    Date = x.Date,
                    DateOpened = x.DateOpened,
                    DateEnd = x.DateEnd,
                    DateClosed = x.DateClosed,
                    Company = x.Company.RagioneSociale,
                    Product = (x.Product != null) ? x.Product.Name: "",
                    Article = (x.Article != null) ? x.Article.SerialNumber : "",
                    Project = (x.Project != null) ? x.Project.Name: "",
                    IdUserAssigned = x.IdUserAssigned,
                    IdCompany = x.IdCompany,
                    IdState = x.IdState,
                    IdUserOpened = x.IdUserOpened,
                    UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                    MinuteWork = x.TicketInterventions.Sum(y=>y.Minute),
                    Invoiced = x.Invoiced,
                    Description = x.Description,
                    ContactName = x.Contact != null ? x.Contact.Name : ""
                    
                    
                });                
                

                var items = ticketModel.ToList();

                foreach (var t in items)
                {
                    t.MinuteWorkFormatted = DateTimeHelper.MinuteFormat(t.MinuteWork);
                    await TicketSetState(t);
                    //t.MinuteWork = t.TicketInterventions.Sum(x => x.Minute);
                }

                var paginationMetadata = new
                {
                    totalCount = count,
                };

                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                
                ObjectView<TicketModel, string> ticketView = new ObjectView<TicketModel, string>();


                ticketView.Total = DateTimeHelper.MinuteFormat(totalWork);
                ticketView.Items = items;



                
                return ticketView;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        //[HttpGet("Details/{id}")]
        //public async Task<ActionResult<Ticket>> GetTicketDetails(int id)
        //{

        //    var ticket = await _context.Tickets.Include(x => x.Company).Include(x => x.TicketType)
        //        .Include(x => x.Article).ThenInclude(x => x.Product).Where(x => x.Id == id).FirstOrDefaultAsync();


        //    if (ticket == null)
        //    {
        //        return NotFound();
        //    }
        //    else if (await _permits.CanGetObject(ticket.IdCompany))
        //    {
        //        await TicketSetState(ticket);

        //        return ticket;



        //    }
        //    else
        //    {
        //        await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
        //        return BadRequest();
        //    }


        //}
        [HttpGet("{id}")]
        public async Task<ActionResult<Ticket>> GetTicket(int id)
        {

            var ticket = await _context.Tickets.Include(x => x.Company).Include(x => x.TicketType)
                .Include(x => x.Article).ThenInclude(x => x.Product).Where(x => x.Id == id).FirstOrDefaultAsync();


            if (ticket == null)
            {
                return NotFound();
            }
            else if (await _permits.CanGetObject(ticket.IdCompany))
            {
                await TicketSetState(ticket);
                
                return ticket;
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return BadRequest();
            }


        }

        // GET: api/Tickets/5
        [HttpGet("Details/{id}")]
        public async Task<ActionResult<TicketModel>> GetTicketDetails(int id)
        {
            
            //var ticket = await _context.Tickets.Include(x => x.Company).Include(x => x.TicketType)
            //    .Include(x => x.Article).ThenInclude(x => x.Product).Where(x => x.Id == id).FirstOrDefaultAsync();

            var tickets =  _context.Tickets.Where(x=>x.Id == id).Include(x=>x.UserOpened).AsQueryable();
            var idLang = await _languageService.GetIdLanguage();
            var ticketModel = await tickets.Select(x => new TicketModel()
            {
                Id = x.Id,
                Date = x.Date,
                DateEnd = x.DateEnd,
                DateOpened = x.DateOpened,
                DateClosed = x.DateClosed,
                Time = x.Time,
                Company = x.Company.RagioneSociale,
                Product = (x.Product != null) ? x.Product.Name : "",
                Article = (x.Article != null) ? x.Article.SerialNumber : "",
                Project = (x.Project != null) ? x.Project.Name : "",
                IdUserAssigned = x.IdUserAssigned,
                IdCompany = x.IdCompany,
                IdState = x.IdState,
                IdUserOpened = x.IdUserOpened,
                UserOpened = (x.UserOpened != null) ? x.UserOpened.NameComplete : "",
                UserAssigned = (x.UserAssigned != null) ? x.UserAssigned.NameComplete : "",
                UserClosed = (x.UserClosed != null) ? x.UserClosed.NameComplete : "",
                MinuteWork = x.TicketInterventions.Sum(y => y.Minute),
                Description = x.Description,
                DescType = (x.TicketType.Languages.Where(x => x.IdLanguage == idLang).Any()) ? x.TicketType.Languages.Where(x => x.IdLanguage == idLang).FirstOrDefault().Name: "",
                TicketType = x.TicketType,
                ContactName = x.Contact != null ? x.Contact.NameComplete : "",
                CloseDescription = x.CloseDescription
                
            }).FirstOrDefaultAsync();

            
            await TicketSetState(ticketModel);

            if (ticketModel == null)
            {
                return NotFound();
            }
            else if (await _permits.CanGetObject(ticketModel.IdCompany))
            {               
                return ticketModel;
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(GetTicket), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return BadRequest();
            }


        }

        [HttpGet("Report/{id}")]
        public async Task<string?> CreateDocPdf(int id)
        {
            try
            {
                var idLang = await _languageService.GetIdLanguage();

                var pdf = await CreatePdf(id, idLang);

                return pdf;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(CreateDocPdf), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


       

        // PUT: api/Tickets/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicket(int id, Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return BadRequest();
            }

            var changeAssigned = await TicketChangeAssigned(id, ticket.IdUserAssigned);

            if (! await _permits.CanEditTicket())
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PutTicket), LogEvent.EventsTypes.Error, "Attempt to Edit without rights");
                return BadRequest();
            }
            _context.Entry(ticket).State = EntityState.Modified;
            if (!await _permits.IsAdmin())
                _context.Entry(ticket).Property(x => x.Invoiced).IsModified = false;

            try
            {
                
                await _context.SaveChangesAsync();

                if (changeAssigned)
                {
                    await SendEmailUserAssigned(ticket);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketExists(id))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

       
        // POST: api/Tickets
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Ticket>> PostTicket(Ticket ticket)
        {
            int day = 3;

            try
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                day = await GetDayBeforeExpired(ticket.Id);

                ticket.DateOpened = DateTime.Now;

                if (ticket.Date == null)
                {
                    ticket.Date = ticket.DateOpened;
                }
                ticket.IdUserOpened = _userManager.GetUserId(User);

                
                ticket.DateExpired = ticket.Date?.AddWorkdays(day);

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                await SendEmailNoticeNewTicket(ticket.Id);

                if (ticket.IdUserAssigned == null)
                {
                    await SendEmailNewTicketToBeAssigned(ticket.Id);
                }
                else
                    await SendEmailUserAssigned(ticket);


                return CreatedAtAction("GetTicket", new { id = ticket.Id }, ticket);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PostTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }

            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(PostTicket), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        

        // DELETE: api/Tickets/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null)
                {
                    return NotFound();
                }

                _context.Tickets.Remove(ticket);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(DeleteTicket), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        [HttpGet("UsersToAssign/{id}")]
        public async Task<List<ApplicationUser>> TicketGetUserToAssign(int id)
        {
            return await _permits.GetUsersCanAssignTicket(id);
        }

        [HttpGet("TypeUsersToAssign/{id}")]
        public async Task<List<ApplicationUser>> TicketTypeGetUserToAssign(int idType)
        {
            return await _permits.GetUsersCanAssignTicketType(idType);
        }

        [HttpPut("TicketClose/{id}")]

        public async Task<IActionResult> TicketClose(int id, TicketClose model)
        {
            try
            {
                if (id != model.Id)
                {
                    return BadRequest();
                }
                else if (await _permits.CanCloseTicket(id))
                {
                    var ticketState = await GetIdState(eTicketStates.Closed);

                    Ticket? ticket = await _context.Tickets.FindAsync(id);
                    if (ticket != null)
                    {
                        ticket.DateClosed = DateTime.Now;
                        ticket.CloseDescription = model.Description;
                        ticket.CloseNote = model.Note;
                        ticket.IdUserClosed = await _permits.IdUser();
                        ticket.Support = model.Support;
                        ticket.Closed = true;
                        ticket.IdState = ticketState?.Id;

                        // NUOVO: Genera e salva embedding
                        try
                        {
                            var ticketText = $"{ticket.Description} {ticket.CloseDescription}";
                            var embedding = await _embeddingService.GenerateEmbeddingAsync(ticketText);
                            ticket.DescriptionEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                        }
                        catch (Exception ex)
                        {
                            await _logEventService.RegisterAsync(nameof(TicketsController), nameof(TicketClose), LogEvent.EventsTypes.Error, ex);
                           
                            // Non bloccare la chiusura se fallisce
                        }

                        await _context.SaveChangesAsync();
                    }
                    return NoContent();
                }
                else
                    return BadRequest();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(TicketClose), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        [HttpPut("TicketReOpen/{IdTicket}")]
        public async Task<ActionResult> ReOpen(int IdTicket, Ticket model)
        {
            try
            {

                if (IdTicket != model.Id)
                {
                    return BadRequest();
                }

                if (await _permits.CanReOpenTicket(IdTicket))
                {
                    var ticket = await _context.Tickets.FindAsync(IdTicket);

                    if (ticket != null)
                    {
                        ticket.Closed = false;
                        await _context.SaveChangesAsync();
                        return NoContent();
                    }
                }
                return NoContent();
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(ReOpen), LogEvent.EventsTypes.Error, ex);
                return BadRequest();
            }
        }
        private bool TicketExists(int id)
        {
            return _context.Tickets.Any(e => e.Id == id);
        }


        private async Task<bool> TicketChangeAssigned(int id, string? idAssigned)
        {
            bool state = false;
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                state = (ticket.IdUserAssigned != idAssigned);

                _context.Entry(ticket).State = EntityState.Detached;

            }
            
            return state;
        }
        private async Task TicketSetState(TicketModel ticket)
        {
            TicketState? ticketState = await GetTicketIdState(ticket);
            ticket.IdState = ticketState?.Id;
            ticket.State = (ticketState?.idState)?.ToString(); //.Description;
            ticket.StateColor = ticketState?.Color;
            
            ticket.Permits = await _permits.TicketPermits(ticket.Id, ticket.IdCompany, ticket.IdUserAssigned);

            if (! await _permits.CanViewInternalData())
                ticket.CloseNote = "";
          
        }

        private async Task TicketSetState(Ticket ticket)
        {
            TicketState? ticketState = await GetTicketIdState(ticket);
            ticket.IdState = ticketState?.Id;
            ticket.StateDesc = (ticketState?.idState)?.ToString(); //.Description;
            ticket.StateColor = ticketState?.Color;

            ticket.Permits = await _permits.TicketPermits(ticket.Id, ticket.IdCompany, ticket.IdUserAssigned);

            if (!await _permits.CanViewInternalData())
                ticket.CloseNote = "";

        }


        [NonAction]
        private IQueryable<Ticket> GetTicketFiltered(IQueryable<Ticket> tickets, TicketTypeSearch filter, string idUser)
        {
            

            switch (filter)
            {
                case TicketTypeSearch.Assigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned != null);
                    break;

                case TicketTypeSearch.NotAssigned:
                    tickets = tickets.Where(x => !x.Closed);
                    tickets = tickets.Where(x => x.IdUserAssigned == null);
                    break;

                case TicketTypeSearch.Expired:
                    tickets = tickets.Where(x => !x.Closed);
                    DateTime date = DateTime.Now.Date;
                    tickets = tickets.Where(x => date > x.DateExpired);
                    break;

                case TicketTypeSearch.Working:
                    tickets = tickets.Where(x => !x.Closed);
                    break;

                case TicketTypeSearch.NewMessage:

                    
                    tickets = tickets.Where(x => x.TicketsChats.Where(y => y.TicketChatReads.Where(z => z.Displayed == false && z.IdUser == idUser).Any()).Any());
                    break;

                case TicketTypeSearch.ToBeInvoiced:
                    tickets = tickets.Where(x => x.Invoiced == false);
                    break;
            }

            return tickets;
        }

        [NonAction]
        private async Task<TicketState?> GetTicketIdState(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);

            if (ticket == null)
                return null;

            return await GetTicketIdState(ticket);
        }

        [NonAction]
        private async Task<TicketState?> GetTicketIdState(Ticket ticket)
        {
           

            if (ticket != null)
            {
                
                if (ticket.Closed)
                    return await GetIdState(eTicketStates.Closed);
                else
                {
                    await CheckTicketExpired(ticket.Id);
                    
                    if (await _permits.IsClient())
                    {
                       return await GetIdState(eTicketStates.Processing);
                    }
                    else if (ticket.DateExpired != null && DateTime.Now.Date > ticket.DateExpired)
                        return await GetIdState(eTicketStates.Expired);

                    else if (ticket.IdUserAssigned != null || ticket.IdGroupAssigned != null)
                        return await GetIdState(eTicketStates.Assigned);
                    else
                        return await GetIdState(eTicketStates.Created);
                }

            }
            return null;
        }

        [NonAction]
        private async Task<TicketState?> GetTicketIdState(TicketModel ticketModel)
        {

            var ticket = await _context.Tickets.FindAsync(ticketModel.Id);

            if (ticket != null)
            {
                if (ticket.Closed)
                    return await GetIdState(eTicketStates.Closed);
                else
                {
                    await CheckTicketExpired(ticket.Id);

                    if (await _permits.IsClient())
                    {
                        return await GetIdState(eTicketStates.Processing);
                    }
                    else if (DateTime.Now.Date > ticket.DateExpired)
                        return await GetIdState(eTicketStates.Expired);

                    else if (ticket.IdUserAssigned != null || ticket.IdGroupAssigned != null)
                        return await GetIdState(eTicketStates.Assigned);
                    else
                        return await GetIdState(eTicketStates.Created);
                }

            }
            return null;
        }

        [NonAction]
        private async Task<TicketState?> GetIdState(eTicketStates state)
        {
            var ticketState = await _context.TicketStates.Where(x => x.State == (int)state).FirstOrDefaultAsync();
            if (ticketState != null)
                ticketState.idState = state;
            return ticketState;
        }


        /// <summary>
        /// Invio delle emails per avvertire la creazione di un Ticket
        /// - Invio all'utente che ha aperto il ticket
        /// - Invio al cliente che ha richiesto il ticket
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        private async Task SendEmailNoticeNewTicket(int idTicket)
        {
            try
            {
                var ticket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x=>x.Id == idTicket);

                if (ticket != null)
                {
                    var userOpened = await _context.Users.FindAsync(ticket.IdUserOpened);

                    var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{idTicket}");

                    var keyValues = new Dictionary<string, string>();
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));

                    if (ticket.Company != null) 
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);
                    
                    if (callbackUrl != null)
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                    if (userOpened != null)
                    {
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), userOpened.NameComplete);


                        var msg = await _emailSenderPlus.SendEmailAsync(new List<string>() { userOpened.Email }, EmailsTypes.NoticeNewTicket, null, keyValues);

                        if (userOpened.PhoneNumber != null && userOpened.PhoneNumber.Length > 0 && msg != null)
                        {
                            await _TelegramService.SendMessage(userOpened.PhoneNumber, msg.TextBody);
                        }
                    }


                }
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailNoticeNewTicket), LogEvent.EventsTypes.Error, ex);
            }

        }

        /// <summary>
        /// Send email to users that can assign ticket
        /// </summary>
        /// <param name="ticket"></param>
        /// <returns></returns>
        private async Task SendEmailNewTicketToBeAssigned(int idTicket)
        {
            try
            {
                
                List<string> phones = new List<string>();


                var ticket = await _context.Tickets.Include(x => x.Company).FirstOrDefaultAsync(x=>x.Id == idTicket);

                if (ticket == null)
                    return;

                List<string> to = new List<string>();
     
                                  
                var users = _context.Users.Where(x => x.Groups.Where(x => x.TicketTypes.Where(y => y.Id == ticket.IdType).Any() || x.TicketTypes.Where(y=>y.Id == ticket.IdType).Any()).Any());

                foreach (var user in users)
                {
                    if (await _permits.CanAssignTicket(user.Id))
                    {
                        if (!to.Contains(user.Email))
                            to.Add(user.Email);

                        if (user.PhoneNumber != null && user.PhoneNumber.Length > 0 && phones.Contains(user.PhoneNumber))
                        {
                            phones.Add(user.PhoneNumber);
                        }
                    }
                }

                var admins = await _permits.GetAdmins();

                foreach (var user in admins)
                {
                    if (await _permits.CanAssignTicket(user.Id))
                    {
                        if (!to.Contains(user.Email))
                            to.Add(user.Email);

                        if (user.PhoneNumber != null && user.PhoneNumber.Length > 0 && phones.Contains(user.PhoneNumber))
                        {
                            phones.Add(user.PhoneNumber);
                        }
                    }
                }
                var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{idTicket}");

                var keyValues = new Dictionary<string, string>();
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);


                if (callbackUrl != null)
                    keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);

                var msg = await _emailSenderPlus.SendEmailAsync(to, EmailsTypes.NoticeNewTicketToBeAssigned, null, keyValues);

                if (msg != null)
                {
                    foreach (var phone in phones)
                    {

                        await _TelegramService.SendMessage(phone, msg.TextBody);

                    }
                }

            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailNewTicketToBeAssigned), LogEvent.EventsTypes.Error, ex);
            }

        }
        private async Task<bool> SendEmailUserAssigned(Ticket ticket)
        {
            try
            {
                if (ticket.IdUserAssigned != null)
                {
                    var userAssigned = await _context.Users.FindAsync(ticket.IdUserAssigned);

                    if (userAssigned != null)
                    {
                        var keyValues = new Dictionary<string, string>();
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Date), ticket.DateOpened.ToString("g"));
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Company), ticket.Company.RagioneSociale);
                        keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Name), userAssigned.NameComplete);

                        var callbackUrl = HttpContext.AbsoluteUrl($"/Tickets/Info/{ticket.Id}");

                        if (callbackUrl != null)
                            keyValues.Add(EmailHelper.KeyWord(EmailHelper.KeyWords.Url), callbackUrl);
                        var msg = await _emailSenderPlus.SendEmailAsync(new List<string>() { userAssigned.UserName }, EmailsTypes.NoticeTicketAssigned, null, keyValues);

                        if (userAssigned.PhoneNumber != null && userAssigned.PhoneNumber.Length > 0 && msg != null)
                        {
                            await _TelegramService.SendMessage(userAssigned.PhoneNumber, msg.TextBody);
                        }

                        return true;
                    }

                }
                return false;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SendEmailUserAssigned), LogEvent.EventsTypes.Error, ex);
                return false;
            }
        }

        [HttpPost("semantic-search")]
        public async Task<ActionResult<CRM.Shared.Models.SemanticSearchResponse>> SemanticSearch([FromBody] CRM.Shared.Models.SemanticSearchRequest request, [FromServices] OpenAIEmbeddingService embeddingService)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProblemDescription))
                {
                    return BadRequest("La descrizione del problema è obbligatoria");
                }

                // Validazione input: minimo 3 caratteri e almeno 2 parole
                var trimmedInput = request.ProblemDescription.Trim();
                if (trimmedInput.Length < 3)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = 0,
                        EmbeddingGenerated = false,
                        Message = "La descrizione deve contenere almeno 3 caratteri"
                    });
                }

                // Controllo parole significative (almeno una parola di 3+ caratteri)
                var words = trimmedInput.Split(new[] { ' ', ',', '.', ';', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                var hasValidWords = words.Any(w => w.Length >= 3 && System.Text.RegularExpressions.Regex.IsMatch(w, @"^[a-zA-Z0-9àèéìòùÀÈÉÌÒÙ]+$"));
                
                if (!hasValidWords)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = 0,
                        EmbeddingGenerated = false,
                        Message = "La query deve contenere almeno una parola significativa (3+ caratteri alfanumerici)"
                    });
                }

                // Genera embedding per la query dell'utente
                var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.ProblemDescription);

                // Verifica che l'embedding generato non sia "nullo" o troppo simile a zero
                var embeddingMagnitude = Math.Sqrt(queryEmbedding.Sum(x => x * x));
                if (embeddingMagnitude < 0.1) // Soglia molto bassa per embedding validi
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        EmbeddingGenerated = false,
                        Message = "La query non contiene informazioni sufficienti per generare una ricerca valida"
                    });
                }

                // Recupera tutti i ticket chiusi con le loro descrizioni
                var query = _context.Tickets
                    .Include(x => x.Company)
                    .Include(x => x.TicketType)
                    .Include(x => x.UserClosed)
                    .Where(x => x.Closed == true && !string.IsNullOrEmpty(x.Description));

                // Filtra per permessi utente
                if (!await _permits.CanAccessOtherCompany())
                {
                    var idCompany = await _permits.GetIdCompany();
                    query = query.Where(x => x.IdCompany == idCompany);
                }

                var closedTickets = await query
                    .Select(x => new
                    {
                        x.Id,
                        x.Description,
                        x.CloseDescription,
                        x.DateClosed,
                        EmbeddingJson = x.DescriptionEmbedding,
                        CompanyName = x.Company.RagioneSociale,
                        Priority = x.Priority != null ? x.Priority.ToString() : "Normal"
                    })
                    .ToListAsync();

                // Calcola similarità per ogni ticket
                var results = new List<CRM.Shared.Models.TicketSimilarityResult>();

                foreach (var ticket in closedTickets)
                {
                    // Skip ticket senza embedding pre-calcolato
                    if (string.IsNullOrEmpty(ticket.EmbeddingJson))
                        continue;

                    try
                    {
                        // Deserializza embedding pre-calcolato
                        var ticketEmbedding = System.Text.Json.JsonSerializer
                            .Deserialize<float[]>(ticket.EmbeddingJson);

                        if (ticketEmbedding == null || ticketEmbedding.Length == 0)
                            continue;

                        var similarity = embeddingService.CalculateCosineSimilarity(
                            queryEmbedding, ticketEmbedding);
                        var percentage = embeddingService.CosineSimilarityToPercentage(similarity);

                        // SOGLIA ALZATA: solo risultati con almeno 60% di similarità
                        var effectiveThreshold = Math.Max(request.MinSimilarityThreshold, 60.0);

                        if (percentage >= effectiveThreshold)
                        {
                            results.Add(new CRM.Shared.Models.TicketSimilarityResult
                            {
                                TicketId = ticket.Id,
                                TicketNumber = $"#{ticket.Id}",
                                Title = ticket.Description?.Substring(0, Math.Min(100, ticket.Description.Length)) + "...",
                                Description = ticket.Description,
                                CustomerName = ticket.CompanyName,
                                SimilarityPercentage = Math.Round(percentage, 2),
                                CosineSimilarity = Math.Round(similarity, 4),
                                ClosedDate = ticket.DateClosed,
                                Solution = ticket.CloseDescription,
                                Priority = ticket.Priority
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log ma continua con altri ticket
                        await _logEventService.RegisterAsync(nameof(TicketsController), 
                            nameof(SemanticSearch), 
                            LogEvent.EventsTypes.Warning, 
                            $"Errore parsing embedding per ticket {ticket.Id}: {ex.Message}");
                        continue;
                    }
                }

                // Ordina per similarità e prendi i top risultati
                var topResults = results
                    .OrderByDescending(x => x.SimilarityPercentage)
                    .Take(request.TopResults)
                    .ToList();

                stopwatch.Stop();

                return new CRM.Shared.Models.SemanticSearchResponse
                {
                    Results = topResults,
                    TotalAnalyzed = closedTickets.Count,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EmbeddingGenerated = queryEmbedding.Length > 0,
                    Message = topResults.Any() 
                        ? $"Trovati {topResults.Count} ticket simili (threshold 60%)" 
                        : "Nessun ticket simile trovato con similarità >= 60%. Prova a descrivere il problema in modo più dettagliato."
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(SemanticSearch), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante la ricerca semantica: {ex.Message}");
            }
        }

        [HttpPost("hybrid-search")]
        public async Task<ActionResult<CRM.Shared.Models.SemanticSearchResponse>> HybridSearch(
            [FromBody] CRM.Shared.Models.SemanticSearchRequest request,
            [FromServices] OpenAIEmbeddingService embeddingService,
            [FromServices] OpenAIChatService chatService)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProblemDescription))
                {
                    return BadRequest("La descrizione del problema è obbligatoria");
                }

                // STEP 1: Validazione AI con GPT
                var validation = await chatService.ValidateITQueryAsync(request.ProblemDescription);
                if (!validation.IsValid)
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = $"❌ {validation.Reason}"
                    });
                }

                // STEP 2: SQL Full-Text Search (veloce e preciso!)
                var sqlResults = await SearchTickets(new TicketFilter
                {
                    Search = request.ProblemDescription,
                    Top = 20,
                    Skip = 0
                });

                if (sqlResults.Value == null || !sqlResults.Value.Any())
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = 0,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = "Nessun ticket trovato con ricerca keyword. Prova con parole chiave diverse."
                    });
                }

                // STEP 3: Re-Ranking con Embeddings sui risultati SQL
                var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.ProblemDescription);
                var ticketIds = sqlResults.Value.Select(x => x.Id).ToList();

                var ticketsForReranking = await _context.Tickets
                    .Include(x => x.Company)
                    .Where(x => ticketIds.Contains(x.Id) && !string.IsNullOrEmpty(x.DescriptionEmbedding))
                    .Select(x => new
                    {
                        x.Id,
                        x.Description,
                        x.CloseDescription,
                        x.DateClosed,
                        EmbeddingJson = x.DescriptionEmbedding,
                        CompanyName = x.Company.RagioneSociale,
                        Priority = x.Priority != null ? x.Priority.ToString() : "Normal"
                    })
                    .ToListAsync();

                var rerankedResults = new List<CRM.Shared.Models.TicketSimilarityResult>();

                foreach (var ticket in ticketsForReranking)
                {
                    try
                    {
                        var ticketEmbedding = System.Text.Json.JsonSerializer
                            .Deserialize<float[]>(ticket.EmbeddingJson);

                        if (ticketEmbedding == null || ticketEmbedding.Length == 0)
                            continue;

                        var similarity = embeddingService.CalculateCosineSimilarity(
                            queryEmbedding, ticketEmbedding);
                        var percentage = embeddingService.CosineSimilarityToPercentage(similarity);

                        rerankedResults.Add(new CRM.Shared.Models.TicketSimilarityResult
                        {
                            TicketId = ticket.Id,
                            TicketNumber = $"#{ticket.Id}",
                            Title = ticket.Description?.Substring(0, Math.Min(100, ticket.Description.Length)) + "...",
                            Description = ticket.Description,
                            CustomerName = ticket.CompanyName,
                            SimilarityPercentage = Math.Round(percentage, 2),
                            CosineSimilarity = Math.Round(similarity, 4),
                            ClosedDate = ticket.DateClosed,
                            Solution = ticket.CloseDescription,
                            Priority = ticket.Priority
                        });
                    }
                    catch (Exception ex)
                    {
                        await _logEventService.RegisterAsync(nameof(TicketsController), 
                            nameof(HybridSearch), 
                            LogEvent.EventsTypes.Warning, 
                            $"Errore re-ranking ticket {ticket.Id}: {ex.Message}");
                        continue;
                    }
                }

                // Ordina per similarità AI e prendi top 20
                var top20Results = rerankedResults
                    .OrderByDescending(x => x.SimilarityPercentage)
                    .Take(20)
                    .ToList();

                if (!top20Results.Any())
                {
                    return Ok(new CRM.Shared.Models.SemanticSearchResponse
                    {
                        Results = new List<CRM.Shared.Models.TicketSimilarityResult>(),
                        TotalAnalyzed = sqlResults.Value.Count(),
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                        Message = "Ticket trovati ma senza embeddings validi per il ranking."
                    });
                }

                // STEP 4: AI VERIFICATION - GPT verifica se esiste una soluzione reale
                var ticketsForVerification = top20Results.Select(t => (
                    TicketId: t.TicketId,
                    Description: t.Description ?? "",
                    Solution: t.Solution ?? "",
                    Similarity: t.SimilarityPercentage
                )).ToList();

                var verification = await chatService.VerifySolutionRelevanceAsync(
                    request.ProblemDescription,
                    ticketsForVerification
                );

                // Se GPT ha trovato un best ticket, mettilo in cima
                List<CRM.Shared.Models.TicketSimilarityResult> finalResults;
                if (verification.HasRelevantSolution && verification.BestTicketId.HasValue)
                {
                    var bestTicket = top20Results.FirstOrDefault(t => t.TicketId == verification.BestTicketId.Value);
                    if (bestTicket != null)
                    {
                        // Rimuovi il best ticket e mettilo in cima
                        finalResults = new List<CRM.Shared.Models.TicketSimilarityResult> { bestTicket };
                        finalResults.AddRange(top20Results.Where(t => t.TicketId != verification.BestTicketId.Value));
                    }
                    else
                    {
                        finalResults = top20Results;
                    }
                }
                else
                {
                    finalResults = top20Results;
                }

                // STEP 5: GPT genera summary per il miglior risultato
                string aiSummary = "";
                if (verification.HasRelevantSolution && verification.BestTicketId.HasValue)
                {
                    var bestTicket = finalResults.First();
                    try
                    {
                        aiSummary = await chatService.GenerateSolutionSummaryAsync(
                            request.ProblemDescription,
                            bestTicket.Description ?? "",
                            bestTicket.Solution ?? ""
                        );
                    }
                    catch
                    {
                        aiSummary = verification.Reason;
                    }
                }

                // Prendi solo i risultati richiesti dall'utente
                var topResults = finalResults.Take(request.TopResults).ToList();

                stopwatch.Stop();

                string finalMessage;
                if (verification.HasRelevantSolution)
                {
                    finalMessage = $"✅ SOLUZIONE TROVATA! {aiSummary}";
                }
                else
                {
                    finalMessage = $"⚠️ Nessuna soluzione diretta trovata. {verification.Reason}. Ti mostro {topResults.Count} ticket simili che potrebbero aiutarti.";
                }

                return new CRM.Shared.Models.SemanticSearchResponse
                {
                    Results = topResults,
                    TotalAnalyzed = sqlResults.Value.Count(),
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    EmbeddingGenerated = true,
                    Message = finalMessage
                };
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsController), nameof(HybridSearch), LogEvent.EventsTypes.Error, ex);
                return Problem($"Errore durante hybrid search: {ex.Message}");
            }
        }

        [HttpPost("generate-embeddings")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<object>> GenerateEmbeddingsForExistingTickets([FromQuery] int batchSize = 10)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Recupera ticket chiusi senza embedding
                var ticketsWithoutEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .OrderByDescending(x => x.DateClosed)
                    .Take(batchSize)
                    .ToListAsync();

                if (!ticketsWithoutEmbedding.Any())
                {
                    return Ok(new
                    {
                        message = "Tutti i ticket chiusi hanno già gli embeddings generati",
                        processed = 0,
                        remaining = 0
                    });
                }

                var totalRemaining = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .CountAsync();

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var ticket in ticketsWithoutEmbedding)
                {
                    try
                    {
                        // Combina descrizione e soluzione
                        var ticketText = $"{ticket.Description} {ticket.CloseDescription ?? ""}";
                        
                        // Genera embedding
                        var embedding = await _embeddingService.GenerateEmbeddingAsync(ticketText);
                        
                        // Salva nel DB
                        ticket.DescriptionEmbedding = System.Text.Json.JsonSerializer.Serialize(embedding);
                        successCount++;

                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(GenerateEmbeddingsForExistingTickets), 
                            LogEvent.EventsTypes.Info, 
                            $"Embedding generato per ticket #{ticket.Id}");
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        var errorMsg = $"Ticket #{ticket.Id}: {ex.Message}";
                        errors.Add(errorMsg);
                        
                        await _logEventService.RegisterAsync(
                            nameof(TicketsController), 
                            nameof(GenerateEmbeddingsForExistingTickets), 
                            LogEvent.EventsTypes.Error, 
                            errorMsg);
                    }
                }

                // Salva modifiche
                await _context.SaveChangesAsync();

                stopwatch.Stop();

                return Ok(new
                {
                    message = $"Elaborazione completata",
                    processed = successCount,
                    errors = errorCount,
                    errorDetails = errors,
                    remainingTickets = totalRemaining - successCount,
                    processingTimeMs = stopwatch.ElapsedMilliseconds,
                    suggestion = totalRemaining - successCount > 0 
                        ? "Esegui nuovamente l'endpoint per processare il prossimo batch" 
                        : "Tutti i ticket sono stati processati!"
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(GenerateEmbeddingsForExistingTickets), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore durante la generazione degli embeddings: {ex.Message}");
            }
        }

        [HttpGet("embeddings-stats")]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<object>> GetEmbeddingsStatistics()
        {
            try
            {
                var totalClosedTickets = await _context.Tickets
                    .Where(x => x.Closed == true)
                    .CountAsync();

                var ticketsWithEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.DescriptionEmbedding))
                    .CountAsync();

                var ticketsWithoutEmbedding = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && !string.IsNullOrEmpty(x.Description)
                        && (x.DescriptionEmbedding == null || x.DescriptionEmbedding == ""))
                    .CountAsync();

                var ticketsWithoutDescription = await _context.Tickets
                    .Where(x => x.Closed == true 
                        && string.IsNullOrEmpty(x.Description))
                    .CountAsync();

                var percentage = totalClosedTickets > 0 
                    ? Math.Round((double)ticketsWithEmbedding / totalClosedTickets * 100, 2) 
                    : 0;

                return Ok(new
                {
                    totalClosedTickets,
                    ticketsWithEmbedding,
                    ticketsWithoutEmbedding,
                    ticketsWithoutDescription,
                    completionPercentage = percentage,
                    ready = ticketsWithoutEmbedding == 0
                });
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(
                    nameof(TicketsController), 
                    nameof(GetEmbeddingsStatistics), 
                    LogEvent.EventsTypes.Error, 
                    ex);
                
                return Problem($"Errore: {ex.Message}");
            }
        }

        private async Task<List<TicketSearchModel>> TicketsSearch(int top, int skip, string txtSearch, int? idCompany, int? idProduct, int? IdArticle)
        {
            try
            {
                string p = txtSearch;

                var select = $"SELECT  FT_TBL.Id as Id,FT_TBL.Description as Description, KEY_TBL.RANK as Rank ";
                var from = $"FROM Tickets AS FT_TBL INNER JOIN FREETEXTTABLE(Tickets, Description, {p}, LANGUAGE N'Italian', 2) AS KEY_TBL ON FT_TBL.Id = KEY_TBL.[KEY]";
                string where = string.Empty;

                if (idCompany != null || idProduct != null || IdArticle != null) 
                {
                    where = "where ";

                    if (idCompany != null)
                        where += $" FT_TBL.IdCompany = {idCompany}";

                    if (idProduct != null)
                        where += $" FT_TBL.IdProduct = {idProduct}";

                    if (IdArticle != null)
                        where += $" FT_TBL.IdArticke = {IdArticle}";

                }

                var orderby = $"ORDER BY KEY_TBL.RANK DESC OFFSET {skip} ROWS FETCH NEXT {top} ROWS ONLY";

                FormattableString sql = $"{select} {from} {where} {orderby}";

                List<TicketSearchModel> tickets = await _context.Tickets.FromSql(sql).Select(x => new TicketSearchModel()
                {
                    Description = x.Description,
                    Id = x.Id,
                    Rank = x.RANK
                }).ToListAsync();

                return tickets;
            }
            catch(Exception ex)
            {
                return new List<TicketSearchModel>();
            }
           
        }


        private async Task CheckTicketExpired(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                
                    int day = await GetDayBeforeExpired(id);

                    if (day > 0 && ticket.Date != null)
                    {
                        ticket.DateExpired = ticket.Date.Value.AddDays(day);
                    }
                    else if (ticket.DateExpired != null)
                        ticket.DateExpired = null;

                    await _context.SaveChangesAsync();
                
            }
        }

        private async Task<int> GetDayBeforeExpired(int id)
        {
            int day = 3;

            var ticket = await _context.Tickets.FindAsync(id);

            if (ticket != null)
            {
                var ticketType = await _context.TicketTypes.FindAsync(ticket.IdType);

                if (ticketType != null)
                {
                    if (ticketType.ExpiredDate > 0)
                    {
                        day = ticketType.ExpiredDate;
                    }
                    else
                    {
                        var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                        if (settings != null)
                        {
                            day = settings.TicketDaysExpired;
                        }
                    }

                }

            }
            return day;
        }

        private async Task<string?> CreatePdf(int id, int? idLanguage)
        {
            try
            {
                Company company;

                Ticket ticket = await _context.Tickets.Include(x => x.UserAssigned).Where(x => x.Id == id).FirstOrDefaultAsync();

                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();

                if (settings != null)
                    company = await _context.Companies?.FirstOrDefaultAsync(x=>x.Id == settings.IdHeadQuarter);
                else
                    company = await _context.Companies.Where(x => x.CompanyType == CompanyTypes.HeadCompany).FirstOrDefaultAsync();
                
                    
                if (ticket == null || company == null)
                    return null;

                HtmlToPdf converter = new HtmlToPdf();
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 30;
                converter.Options.MarginRight = 30;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;

                converter.Options.DisplayFooter = true;
                converter.Footer.DisplayOnEvenPages = true;
                converter.Footer.DisplayOnOddPages = true;
                converter.Footer.DisplayOnFirstPage = true;

                converter.Options.DisplayHeader = true;
                converter.Header.DisplayOnFirstPage = true;
                converter.Header.DisplayOnEvenPages = true;
                converter.Header.DisplayOnOddPages = true;

                converter.Header.Height = 45;


                converter.Options.RenderingEngine = RenderingEngine.Blink;

               
                string headerUrl = HttpContext.AbsoluteUrl("/Reports/Header",
                        new { id = company.Id });

                PdfHtmlSection headerHtml = new PdfHtmlSection(headerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;

                converter.Header.Add(headerHtml);
                
                string footerUrl = HttpContext.AbsoluteUrl("/Reports/Footer");

                PdfHtmlSection footerHtml = new PdfHtmlSection(footerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;

                PdfTextSection text = new PdfTextSection(0, 10,
                        "Page: {page_number} of {total_pages}  ",
                       new System.Drawing.Font("Arial", 8));

                text.HorizontalAlign = PdfTextHorizontalAlign.Right;

                converter.Footer.Add(footerHtml);
                converter.Footer.Add(text);
                converter.Footer.FirstPageNumber = 1;

                var absUrl = HttpContext.AbsoluteUrl("/Reports/TicketReport", new { id = id, idLanguage = idLanguage });

                converter.Options.MinPageLoadTime = 1;
                converter.Options.MaxPageLoadTime = 300;
                PdfDocument doc = converter.ConvertUrl(absUrl);

                // save pdf document 
                var path = _archiveService.GetPath($"{Guid.NewGuid()}.pdf");
                doc.Save(path);

                
                var bytes = System.IO.File.ReadAllBytes(path);

                
                doc.Close();


                var s = Convert.ToBase64String(bytes);
                
                System.IO.File.Delete(path);

                return s;
            }

            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(CreatePdf), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

    }
}
