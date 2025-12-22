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

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TalksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public TalksController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
        }

        // GET: api/Talks
        [HttpGet]
        public async Task<ActionResult<ObjectView<TalkModel, decimal>>> GetTalk([FromQuery]TalkFilter args)
        {
            try
            {
                IQueryable<Talk> talks = _context.Talks;

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


                
                var talksModel = talks.Select(x => new TalkModel()
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

                ObjectView<TalkModel, decimal> talkView = new ObjectView<TalkModel, decimal>();


                talkView.Total = total;
                talkView.Items = await talksModel.ToListAsync();

                foreach (var t in talkView.Items)
                {
                    t.Permits = await _permits.TalkPermits(t.Id);
                }

                return talkView;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TalksController), nameof(GetTalk), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Talks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Talk>> GetTalk(int id)
        {
            var talk = await _context.Talks.FindAsync(id);

            if (talk == null)
            {
                return NotFound();
            }

            return talk;
        }


        // GET: api/Tickets/5
        [HttpGet("Details/{id}")]
        public async Task<ActionResult<TalkModel>> GetTalkDetails(int id)
        {

            var talk =  _context.Talks.Include(x=>x.Company).Where(x=>x.Id == id).AsQueryable();

            var talkModel = await talk.Select(x => new TalkModel()
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
            else if (await _permits.CanGetTalk(talkModel.Id))
            {
                talkModel.Permits = await _permits.TalkPermits(id);
                return talkModel;
            }
            else
            {
                await _logEventService.RegisterAsync(nameof(TalksController), nameof(GetTalkDetails), LogEvent.EventsTypes.Error, GlobalMessages.PermitsErrors);
                return Problem(GlobalMessages.PermitsErrors);
            }


        }

        // PUT: api/Talks/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTalk(int id, Talk talk)
        {
            try
            {
                if (id != talk.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditTalk(id))
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(talk).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TalksController), nameof(PutTalk), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TalkExists(id))
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
        public async Task<ActionResult<Talk>> PostTalk(Talk talk)
        {
            if (await _permits.CanInsertTalk())
            {
                talk.Date = DateTime.Now;
                talk.IdUser = await _permits.IdUser();
                _context.Talks.Add(talk);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetTalk", new { id = talk.Id }, talk);
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        // DELETE: api/Talks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTalk(int id)
        {
            var talk = await _context.Talks.FindAsync(id);
            if (talk == null)
            {
                return NotFound();
            }

            _context.Talks.Remove(talk);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TalkExists(int id)
        {
            return _context.Talks.Any(e => e.Id == id);
        }
    }
}
