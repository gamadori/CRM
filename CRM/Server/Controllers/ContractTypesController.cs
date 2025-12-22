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
using Newtonsoft.Json;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authorization;
using CRM.Client.Services;
using AutoMapper.Internal;
using CNM.Authorize;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ContractTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IPermitsService _permits;

        private readonly ILogEventService _logEventService;


        public ContractTypesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService )
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permitsService;
        }
      
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ContractType>>> GetContractTypes([FromQuery] ContractTypeFilter args)
        {
            try
            {
                int totalPage = 1;

                var items = _context.ContractTypes.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderBy(x => x.Name);

                if (args?.Filter != null && args.Filter.Any())
                {
                    items = items.Where(args.Filter);
                }

                

                int count = items.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    items = items.Skip(args.Skip.Value).Take(args.Top.Value);
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
                var list = await items.ToListAsync();

                
                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ContractTypesController), nameof(GetContractTypes), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/TicketTypes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ContractType>> GetContractType(int id)
        {
            var item = await _context.ContractTypes.FindAsync(id);

            if (item == null)
            {
                return new ContractType();
            }
            item.Permits = await _permits.ContractTypePermits();

            return item;
        }

        // PUT: api/TicketTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutContractType(int id, ContractType item)
        {

            item.DiscountedPrice = item.Price - (item.Price * item.Discount) / 100; 

            if (id != item.Id)
            {
                return BadRequest();
            }

            _context.Entry(item).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ContractTypeExists(id))
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

        // POST: api/TicketTypes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPost]
        public async Task<ActionResult<ContractType>> PostContractType(ContractType item)
        {
            _context.ContractTypes.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetContractType", new { id = item.Id }, item);
        }

        // DELETE: api/TicketTypes/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContractType(int id)
        {
            var item = await _context.ContractTypes.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ContractTypes.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ContractTypeExists(int id)
        {
            return _context.ContractTypes.Any(e => e.Id == id);
        }
    }
}
