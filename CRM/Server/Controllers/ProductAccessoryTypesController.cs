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
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductAccessoryTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogEventService _logEventService;
        private readonly IPermitsService _permits;
        private readonly ILanguagesService _languageService;
        public ProductAccessoryTypesController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permits, ILanguagesService languageService)
        {
            _context = context;
            _logEventService = logEventService;
            _permits = permits;
            _languageService = languageService; 
        }

        // GET: api/Deals
        [HttpGet]
        public async Task<ActionResult<List<ProductAccessoryTypeModel>>> Get([FromQuery]ProductAccessoryTypeFilter args)
        {
            try
            {
                IQueryable<ProductAccessoryTypeModel> items;

                items = from prodAcc in _context.ProductAccessoryTypes
                        join prod in _context.Products on prodAcc.IdProduct equals prod.Id
                        join acc in _context.AccessoryTypes on prodAcc.IdAccessoryType equals acc.Id                        
                        where prod.Id == args.IdProduct
                        select new ProductAccessoryTypeModel()
                        {
                            Id = prodAcc.Id,
                            Name = prodAcc.Name,
                            AccTypeName = acc.Name,
                            ProdName = prod.Name
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
                    items = items.OrderBy(x => x.Name);

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


                var list = items != null ? await items.ToListAsync() : new List<ProductAccessoryTypeModel>();


                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductAccessoryTypesController), nameof(Get), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Deals/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductAccessoryType>> GetItem(int id)
        {
            var accessoryType = await _context.ProductAccessoryTypes.FindAsync(id);


            if (accessoryType == null)
            {
                return NotFound();
            }

            accessoryType.Permits = await _permits.AccesoryTypePermits();

            return accessoryType;
        }

        // GET: api/Deals/5
        [HttpGet("details/{id}")]
        public async Task<ActionResult<ProductAccessoryTypeModel>> GetItemDetails(int id)
        {
            var items = from prodAcc in _context.ProductAccessoryTypes.Where(x=>x.Id== id)
                    join acc in _context.AccessoryTypes on prodAcc.IdAccessoryType equals acc.Id
                    join prod in _context.Products on prodAcc.IdProduct equals prod.Id
                    select new ProductAccessoryTypeModel()
                    {
                        Id = prodAcc.Id,
                        Name = prodAcc.Name,
                        AccTypeName = acc.Name,
                        ProdName = prod.Name
                    };


            var item = items.FirstOrDefault();

            if (item != null)
                item.Permits = await _permits.AccesoryTypePermits();

            return item != null ? item : new ProductAccessoryTypeModel();
        }



        // PUT: api/Deals/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccessoryType(int id, ProductAccessoryType item)
        {
            try
            {
                if (id != item.Id)
                {
                    return BadRequest();
                }

                if (!await _permits.CanEditAccessoryType())
                    return Problem(GlobalMessages.PermitsErrors);

                
                _context.Entry(item).State = EntityState.Modified;
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductAccessoryTypesController), nameof(PutAccessoryType), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProdTypeAccessoryExists(id))
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
        public async Task<ActionResult<ProductAccessoryType>> PostAccessoryType(ProductAccessoryType item)
        {
            try
            {
                if (await _permits.CanInsertAccessoryType())
                {

                    _context.ProductAccessoryTypes.Add(item);
                    await _context.SaveChangesAsync();

                    return CreatedAtAction("GetItem", new { id = item.Id }, item);
                }
                else
                    return Problem(GlobalMessages.PermitsErrors);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductAccessoryTypesController), nameof(PostAccessoryType), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // DELETE: api/Deals/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            var item = await _context.ProductAccessoryTypes.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ProductAccessoryTypes.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProdTypeAccessoryExists(int id)
        {
            return _context.ProductAccessoryTypes.Any(e => e.Id == id);
        }
    }
}
