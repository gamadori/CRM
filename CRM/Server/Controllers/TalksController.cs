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
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DealsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public DealsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<ObjectView<DealModel, decimal>>> GetDeal([FromQuery]DealFilter args)
        {
            try
            {
                IQueryable<Deal> talks = _context.Deals;

                if (!(await _permits.BelongsToMainCompany() && await _permits.IsStandardUser()))
                {
                    return Problem("No Permits");
                }

                if (args.Filter != null)
                {
                    talks = talks.Where(args.Filter);
                }

                if (args.OrderBy != null)
                {
                    talks = talks.OrderBy(args.OrderBy);
                }
                else
                    talks = talks.OrderByDescending(x => x.Date);

                int count = talks.Count();
                decimal total = talks.Sum(x => x.Amount);

                if (talks != null && args?.Skip != null && args.Top != null)
                {
                    talks = talks.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                
                var talksModel = talks.Select(x => new DealModel()
                {
                    Id = x.Id,
                    Date = x.Date,
                    Name = x.Name,
                    Amount = x.Amount,
                    Company = (x.Company != null) ? x.Company.RagioneSociale : "",
                    Contact = (x.Contact != null) ? x.Contact.NameComplete : "",
                    User = (x.User != null) ? x.User.NameComplete : "",
                    IdCompany = x.IdCompany,
                    IdContact = x.IdContact,
                    IdUser = x.IdUser,
                    Note = x.Note,
                    Phase = x.Phase,
                    State = x.State,
                    Target = x.Target,
                    DateClosed = x.DateClosed
                   
                });

                var paginationMetadata = new
                {
                    totalCount = count,
                };

                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

                ObjectView<DealModel, decimal> talkView = new ObjectView<DealModel, decimal>();


                talkView.Total = total;
                talkView.Items = await talksModel.ToListAsync();

                foreach (var t in talkView.Items)
                {
                    t.Permits = await _permits.DealPermits(t.Id);
                }

                return talkView;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetDeal), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Deals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Deal>> GetDeal(int id)
        {
            var deal = await _context.Deals.FindAsync(id);

            if (deal == null)
            {
                return NotFound();
            }

            return deal;
        }


        // GET: api/Tickets/5
        [HttpGet("Details/{id}")]
        public async Task<ActionResult<DealModel>> GetDealDetails(int id)
        {

            var deal =  _context.Deals.Include(x=>x.Company).Where(x=>x.Id == id).AsQueryable();

            var talkModel = await deal.Select(x => new DealModel()
            {
                Id = x.Id,
                Date = x.Date,
                Name = x.Name,  
                Amount = x.Amount,
                Company = (x.Company != null) ? x.Company.RagioneSociale : "",
                Contact = (x.Contact != null) ? x.Contact.NameComplete : "",
                User = (x.User != null) ? x.User.NameComplete : "",
                IdCompany = x.IdCompany,
                IdContact = x.IdContact,
                IdUser = x.IdUser,
                Note = x.Note,
                Phase = x.Phase,
                State = x.State,
                Target = x.Target,
                DateClosed = x.DateClosed
              

            }).FirstOrDefaultAsync();


            

            if (talkModel == null)
            {
                return NotFound();
            }
            else if (await _permits.CanGetDeal(talkModel.Id))
            {
                talkModel.Permits = await _permits.DealPermits(id);
                return talkModel;
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(GetDealDetails), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return Problem(GlobalMessages.PermitsErrors);
            }


        }

        // PUT: api/Deals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDeal(int id, Deal deal)
        {
            try
            {
                if (id != deal.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditDeal(id))
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(deal).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(DealsController), nameof(PutDeal), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DealExists(id))
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

        // POST: api/Deals
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Deal>> PostDeal(Deal deal)
        {
            if (await _permits.CanInsertDeal())
            {
                deal.Date = DateTime.Now;
                deal.IdUser = await _permits.IdUser();
                _context.Deals.Add(deal);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetDeal", new { id = deal.Id }, deal);
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        // DELETE: api/Deals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDeal(int id)
        {
            var deal = await _context.Deals.FindAsync(id);
            if (deal == null)
            {
                return NotFound();
            }

            _context.Deals.Remove(deal);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool DealExists(int id)
        {
            return _context.Deals.Any(e => e.Id == id);
        }
    }
}
