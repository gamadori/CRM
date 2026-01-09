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
    public class AccessoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;

        public AccessoriesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<List<Accessory>>> Get([FromQuery]AccessoryFilter args)
        {
            try
            {
                IQueryable<Accessory> accessories = _context.Accessories.Include(x=>x.AccessoryType);

                
              
                if (args.IdAccessoryType != null)
                {
                    accessories = accessories.Where(x => x.IdAccessoryType == args.IdAccessoryType);
                }
                if (args.Filter != null)
                {
                    accessories = accessories.Where(args.Filter);
                }


                if (args.OrderBy != null)
                {
                    accessories = accessories.OrderBy(args.OrderBy);
                }
                else
                    accessories = accessories.OrderByDescending(x => x.Name);

                int count = accessories.Count();
                
                if (accessories != null && args?.Skip != null && args.Top != null)
                {
                    accessories = accessories.Skip(args.Skip.Value).Take(args.Top.Value);
                }


                var paginationMetadata = new
                {
                    totalCount = count,
                };

                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
               

                

                return await accessories.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoriesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Deals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Accessory>> GetAccessory(int id)
        {
            var accessory = await _context.Accessories.FindAsync(id);

            if (accessory == null)
            {
                return NotFound();
            }

            accessory.Permits = await _permits.AccesoryTypePermits();
            return accessory;
        }

        [HttpGet("Details/{id}")]
        public async Task<ActionResult<Accessory>> GetAccessoryDetails(int id)
        {
            var items =
                     from acc in _context.Accessories.Where(x => x.Id == id)
                     join type in _context.AccessoryTypes on acc.IdAccessoryType equals type.Id
                     
                     select new Accessory()
                     {
                         Id = acc.Id,
                         Name = acc.Name,
                         Description = acc.Description,
                         Code = acc.Code,
                         SupplierCode = acc.SupplierCode,
                        Type = type.Name

                     };

            var item = items.FirstOrDefault();

            if (item != null)
                item.Permits = await _permits.AccesoryPermits();

            return item != null ? item : new Accessory();
        }


        // PUT: api/Deals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccessory(int id, Accessory accessory)
        {
            try
            {
                if (id != accessory.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditAccessory())
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(accessory).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(AccessoriesController), nameof(PutAccessory), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AccessoryExists(id))
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
        public async Task<ActionResult<Deal>> PostAccessory(Accessory accessory)
        {
            if (await _permits.CanInsertAccessoryType())
            {
                accessory.Date = DateTime.Now;
                accessory.IdUser = await _permits.IdUser();
                _context.Accessories.Add(accessory);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetAccessory", new { id = accessory.Id }, accessory);
            }
            else
                return Problem(GlobalMessages.PermitsErrors);
        }

        // DELETE: api/Deals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccessory(int id)
        {
            var accessory = await _context.Accessories.FindAsync(id);
            if (accessory == null)
            {
                return NotFound();
            }

            _context.Accessories.Remove(accessory);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AccessoryExists(int id)
        {
            return _context.Accessories.Any(e => e.Id == id);
        }
    }
}
