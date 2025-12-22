using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using System.Linq.Dynamic.Core;
using CRM.Client.Pages.DashBoard;
using Newtonsoft.Json;
using CRM.Server.Services;
using Microsoft.CodeAnalysis.Host;
using System.Drawing;
using Microsoft.CodeAnalysis.CSharp;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContractTypeTicketTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly ILanguagesService _languageService;
        public ContractTypeTicketTypesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits, ILanguagesService languageService)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            _languageService = languageService; 
        }

        // GET: api/Talks
        [HttpGet]
        public async Task<ActionResult<List<ContractTypeTicketTypeModel>>> Get([FromQuery] ContractTypeTicketTypeFilter args)
        {
            try
            {
                IQueryable<ContractTypeTicketTypeModel> items;

                items = from contractTicket in _context.ContractTypeTicketTypes
                        join
                        contract in _context.ContractTypes on contractTicket.IdContractType equals contract.Id
                        join ticket in _context.TicketTypes on contractTicket.IdTicketType equals ticket.Id
                        where contractTicket.IdContractType == args.IdContractType
                        select new ContractTypeTicketTypeModel()
                        {
                            Id = contractTicket.Id,
                            NumIntervention = contractTicket.NumIntervention,
                            ContractTypeName = contract.Name,
                            TicketTypeName = ticket.Desc,
                            IdContractType = contract.Id,
                            IdTicketType = ticket.Id,
                            Unlimited = contractTicket.Unlimited,
                        };

                
                
                if (args.Filter != null)
                {
                    items = items.Where(args.Filter);
                }

                if (args.OrderBy != null)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.TicketTypeName);

                int count = items.Count();
                
                if (items != null && args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new
                {
                    totalCount = count,
                };

                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));


                var list = items != null ? await items.ToListAsync() : new List<ContractTypeTicketTypeModel>();


                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContractTypeTicketTypesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Talks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ContractTypeTicketType>> GetItem(int id)
        {
            var item = await _context.ContractTypeTicketTypes.FindAsync(id);


            if (item == null)
            {
                return NotFound();
            }


            return item;
        }

        // GET: api/Talks/5
        [HttpGet("details/{id}")]
        public async Task<ActionResult<ContractTypeTicketTypeModel>> GetItemDetails(int id)
        {
            var items = from contractTicket in _context.ContractTypeTicketTypes
                    join
                    contract in _context.ContractTypes on contractTicket.IdContractType equals contract.Id
                    join ticket in _context.TicketTypes on contractTicket.IdTicketType equals ticket.Id
                    where contractTicket.Id == id
                    select new ContractTypeTicketTypeModel()
                    {
                        Id = contractTicket.Id,
                        NumIntervention = contractTicket.NumIntervention,
                        ContractTypeName = contract.Name,
                        TicketTypeName = ticket.Desc,
                        IdContractType = contract.Id,
                        IdTicketType = ticket.Id,
                        Unlimited = contractTicket.Unlimited
                    };

            

            var item = items.FirstOrDefault();

            if (item != null)
                item.Permits = await _permits.ContractTypePermits();

            return item != null ? item : new ContractTypeTicketTypeModel();
        }



        // PUT: api/Talks/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(int id, ContractTypeTicketType item)
        {
            try
            {
                if (id != item.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditContractType())
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(item).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContractTypeTicketTypesController), nameof(PutItem), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Talks
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ProductAccessoryType>> PostItem(ContractTypeTicketType item)
        {
            try
            {
                if (await _permits.CanInsertContractType())
                {

                    _context.ContractTypeTicketTypes.Add(item);
                    await _context.SaveChangesAsync();

                    return CreatedAtAction("GetItem", new { id = item.Id }, item);
                }
                else
                    return Problem(GlobalMessages.PermitsErrors);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContractTypeTicketTypesController), nameof(PostItem), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // DELETE: api/Talks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.ContractTypeTicketTypes.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ContractTypeTicketTypes.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ItemExists(int id)
        {
            return _context.ProductAccessoryTypes.Any(e => e.Id == id);
        }
    }
}
