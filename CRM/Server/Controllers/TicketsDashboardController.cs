using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using CRM.Shared.Helper;
using CRM.Client.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsDashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        public TicketsDashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _userManager = userManager;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        // GET: api/<TicketsDashboard>
        [HttpGet]
        public async Task<ActionResult<TicketDashBoardModel>> Get([FromQuery] TicketDashBoardModelFilter filter)
        {
            int? idCompany = null;
            string idUser = await _permitsService.IdUser();

            TicketDashBoardModel model = new TicketDashBoardModel();
            DateTime date = DateTime.Now.Date;

            model.IsClient = !( await _permitsService.IsAdmin() || await _permitsService.IsSuperUser());

            var tickets = _context.Tickets.AsQueryable();

            
            if (!await _permitsService.CanAccessOtherCompany())
            {
               
                idCompany = await _permitsService.GetIdCompany();
                tickets = tickets.Where(x => x.IdCompany == idCompany);
            }
            else if (model.IsClient)
            {
                // Puo visualizzare solo i dati personali
                filter.IdUser = idUser;
            }

            if (filter?.IdUser != null)
            {
                tickets = tickets.Where(x=>x.AssignedUsers.Where(u=>u.IdUser == filter.IdUser).Any() || x.IdUserAssigned == filter.IdUser);  
            }
            /* Ricerca dei ticket non chiusi */
            
            model.TicketsClosed = await tickets.Where(x => x.Closed == true).CountAsync();

            var items = tickets.Where(x=>x.Closed != true);

            if (filter?.IdUser != null)
                items = items.Where(x => x.IdUserAssigned == filter.IdUser);

            if (model.IsClient)
                model.TicketsWorking = await items.CountAsync();
            else
            {
                model.TicketsWorking = await items.Where(x => x.IdUserAssigned != null).CountAsync();
                model.TicketsExpired = await items.Where(x => date > x.DateExpired).CountAsync();
            }
            
            /* Ricerca dei Ticket che possono essere assegnati dall'utente */
            if (await _permitsService.CanAssignTicket())
            {
                model.TicketsNotAssigned = await tickets.Where(x => !x.Closed && x.IdUserAssigned == null).CountAsync();
            }
            else
                model.TicketsNotAssigned = 0;

            model.TicketAssigned = await tickets.Where(x => x.IdUserAssigned == idUser).CountAsync();
            
            if (await _permitsService.IsAdmin(idUser))
            {
                model.UsersNeedConfirm = await _context.Users.Where(x =>!x.IsDeleted && x.AdminConfirmed == false).CountAsync();
            }
            else
                model.UsersNeedConfirm = 0;

            model.ChatMessageToRead = await _context.TicketChatReads.Where(x => x.IdUser == idUser && x.Displayed == false).CountAsync();

            // Conta interventi con firma in attesa di conferma (Pending)
            var interventionsQuery = _context.TicketsInterventions.AsQueryable();
            
            if (idCompany.HasValue)
            {
                // Filtra per azienda se necessario
                interventionsQuery = interventionsQuery.Where(x => x.Ticket.IdCompany == idCompany);
            }

            if (filter?.IdUser != null)
            {
                // Filtra per utente se specificato
                interventionsQuery = interventionsQuery.Where(x => x.IdUser == filter.IdUser);
            }

            model.InterventionsPendingSignature = await interventionsQuery
                .Where(x => x.SignatureStatus == CRM.Shared.SignatureStatus.Pending && 
                           !string.IsNullOrEmpty(x.CustomerSignature))
                .CountAsync();

            return model;
        }

        [HttpGet("GetClient")]
        public async Task<ActionResult<object>> GetClient()
        {
            string filter = null;
            TicketDashBoardModel model = new TicketDashBoardModel();
            var queryString = Request.Query;
            string order;

            var idCompany = await _permitsService.GetIdCompany();

            var tickets = _context.Tickets.Where(x=>x.IdCompany == idCompany);

            var stateClosed = await _context.TicketStates.Where(x => x.State == (int)eTicketStates.Closed).FirstOrDefaultAsync();

            if (stateClosed != null)
            {
                model.TicketsWorking = await tickets.Where(x=>x.IdState != stateClosed.Id).CountAsync();
                model.Tickets = await tickets.OrderBy(x=>x.DateOpened).ToListAsync();
            }
            try
            {



                if (queryString.Keys.Contains("$filter"))
                {
                    filter = queryString["$filter"];

                    tickets = SyncHelper.GetFilterPredicate(tickets, filter);


                }

                if (queryString.Keys.Contains("$orderby"))
                {
                    order = queryString["$orderby"];
                    tickets = tickets.OrderBy(order);

                }

                if (queryString.Keys.Contains("$inlinecount"))
                {

                    StringValues Skip;
                    StringValues Take;
                    int skip = (queryString.TryGetValue("$skip", out Skip)) ? Convert.ToInt32(Skip[0]) : 0;
                    int top = (queryString.TryGetValue("$top", out Take)) ? Convert.ToInt32(Take[0]) : tickets.Count();

                    IQueryable<Ticket> items;

                    if (top == 0)
                    {
                        items = tickets;

                    }
                    else
                        items = tickets.Skip(skip).Take(top);
                    return new { Items = items, Count = tickets.Count() };
                }
                else
                {

                    return tickets.ToList();
                }
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsDashboardController), nameof(GetClient), LogEvent.EventsTypes.Error, ex.Message);
                return BadRequest(Problem(ex.Message, ex.StackTrace, ErrorsHelper.ErrorGeneric));
            }
        }


        // POST api/<TicketsDashboard>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<TicketsDashboard>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<TicketsDashboard>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
