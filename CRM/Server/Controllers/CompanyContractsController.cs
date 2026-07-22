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
using System.Net.WebSockets;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using CNM.Authorize;
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyContractsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly ILanguagesService _languageService;
        public CompanyContractsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits, ILanguagesService languageService)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            _languageService = languageService; 
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<List<CompanyContract>>> Get([FromQuery] CompanyContractFilter args)
        {
            try
            {
                


                var items = from companyContract in _context.CompanyContracts
                            join
                            contract in _context.ContractTypes on companyContract.IdContractType equals contract.Id
                            select new CompanyContract()
                            {
                                Id = companyContract.Id,
                                DateFrom = companyContract.DateFrom,
                                DateTo = companyContract.DateTo,
                                IdContractType = companyContract.IdContractType,
                                Duration = companyContract.Duration,
                                Price = companyContract.Price,
                                Suspended = companyContract.Suspended,
                                ContractName = contract.Name,
                                Active = CompanyContractActive(companyContract),
                                Enabled = companyContract.Enabled
                            };

                if (args.Active != null && args.Active == true)
                {
                    items = items.Where(x => x.DateTo >= DateTime.Now.Date && x.Enabled);
                }
                if (args.Filter != null)
                {
                    items = items.Where(args.Filter);
                }

                if (args.OrderBy != null)
                {
                    items = items.OrderBy(args.OrderBy);
                }
                else
                    items = items.OrderByDescending(x => x.DateFrom);

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


                var list = items != null ? await items.ToListAsync() : new List<CompanyContract>();


                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompanyContractsController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Deals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CompanyContract>> GetItem(int id)
        {
            var item = await _context.CompanyContracts.FindAsync(id);



            if (item == null)
            {
                return NotFound();
            }

            var contractType = await _context.ContractTypes.FindAsync(item.IdContractType);
            item.Active = CompanyContractActive(item);
            item.ContractName = contractType?.Name; 
            item.Permits = await _permits.ContractTypePermits();

            return item;
        }

        // GET: api/Deals/5
        [HttpGet("details/{id}")]
        public async Task<ActionResult<CompanyContract>> GetItemDetails(int id)
        {
            var item = await _context.CompanyContracts.FindAsync(id);

            if (item != null)
            {
                item.Active = CompanyContractActive(item);
                item.Permits = await _permits.AccesoryTypePermits();
            }
            return item != null ? item : new CompanyContract();
        }



        // PUT: api/Deals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<IActionResult> PutContract(int id, CompanyContract item)
        {
            try
            {
                if (id != item.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanWriteCompanyData(item.IdCompany))
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(item).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompanyContractsController), nameof(PutContract), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanyContractExists(id))
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

        

        [HttpPost("check")]
        public async Task<ActionResult<List<CompanyContract>>> CheckContractsActive(CompanyContract item)
        {
            DateTime date = DateTime.Today;
            var items = from companyContract in _context.CompanyContracts.Where(x => x.IdCompany == item.IdCompany && x.Enabled == true && x.DateTo >= item.DateFrom)
                        join
                        contract in _context.ContractTypes on companyContract.IdContractType equals contract.Id
                        select new CompanyContract()
                        {
                            Id = companyContract.Id,
                            DateFrom = companyContract.DateFrom,
                            DateTo = companyContract.DateTo,
                            IdContractType = companyContract.IdContractType,
                            Duration = companyContract.Duration,
                            Price = companyContract.Price,
                            Suspended = companyContract.Suspended,
                            ContractName = contract.Name,
                            Active = date > companyContract.DateTo &&  companyContract.Enabled,
                            Enabled = contract.Enabled
                        };

            return await items.ToListAsync();
        }

        // POST: api/Deals
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<ActionResult<CompanyContract>> PostContract(CompanyContract item)
        {
            try
            {
                if (await _permits.CanWriteCompanyData(item.IdCompany))
                {
                    var contracts = await _context.CompanyContracts.Where(x => x.IdCompany == item.IdCompany && x.Enabled == true).ToListAsync();

                    if (contracts.Any())
                    {
                        if (!item.Confirm)
                        { 
                            return Problem("There is already a contract enabled");
                        }

                    }
                    foreach (var contract in contracts)
                    {
                        contract.Enabled = false;
                        await _context.SaveChangesAsync();
                    }
                    item.Enabled = true;
                    _context.CompanyContracts.Add(item);
                    await _context.SaveChangesAsync();

                    return CreatedAtAction("GetItem", new { id = item.Id }, item);
                }
                else
                    return Problem(GlobalMessages.PermitsErrors);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompanyContractsController), nameof(PostContract), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // DELETE: api/Deals/5
        [HttpDelete("{id}")]
        [AuthorizeRole(ePolicy.StandardRole)]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.CompanyContracts.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            if (!await _permits.CanWriteCompanyData(item.IdCompany))
                return Forbid();

            _context.CompanyContracts.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        //public async Task<List<ContractTicketTypeDetails>> ContractsDetails(int idCompany)
        //{
        //    var contracts = CompanyContractsActive(idCompany);

        //    var contractTycketTypes = _context.ContractTypeTicketTypes.Where(x => contracts.Where(y => y.IdContractType == x.IdContractType).Any());

        

        //    var ticketTypes = contractTycketTypes.GroupBy(x => x.IdTicketType).Select(x => new ContractTicketTypeDetails() { IdTicketType = x.Key, NumAvaible = x.Sum(x => x.NumIntervention) });



        //    foreach (var contract in contracts)
        //    {
        //        contract.
        //    }
            
        //    foreach (var ticketType in ticketTypes)
        //    {
        //        ticketType.NumUsed = ticket.Where(x => x.IdType == ticketType.IdTicketType).Count();
        //    }

        //}

        private IQueryable<CompanyContract> CompanyContractsActive(int idCompany)
        {
            return _context.CompanyContracts.Where(x => x.DateTo >= DateTime.Now.Date && x.Suspended == false && x.Enabled == true);
        }

        private IQueryable<ContractTypeTicketType> CompanyContractTypesActive(int idCompany)
        {
            var contracts = CompanyContractsActive(idCompany);

            return _context.ContractTypeTicketTypes.Where(x => contracts.Where(y => y.IdContractType == x.IdContractType).Any());
        }


        private static bool CompanyContractActive(CompanyContract companyContract)
        {
            return companyContract.DateTo >= DateTime.Today && !companyContract.Suspended && companyContract.Enabled;
        }
        private bool CompanyContractExists(int id)
        {
            return _context.CompanyContracts.Any(e => e.Id == id);
        }
    }
}
