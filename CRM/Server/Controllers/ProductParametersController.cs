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
using CRM.Shared.Helper;
using CRM.Server.Services;
using Microsoft.AspNetCore.Authorization;
using CNM.Authorize;
using Microsoft.Extensions.Hosting;
using AGUtility.Extensions;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductParametersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;

        private readonly IPermitsService _permitsService;
        public ProductParametersController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService)
        {
            _context = context;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }


        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductParameter>>> GetProductParameters([FromQuery] ProductParameterFilter args)
        {
            int totalPage = 0;

            var items =_context.ProductParameters.AsQueryable();

            if (args.Filter != null && args.Filter.Length > 0)
            {
                items = items.Where(args.Filter);
            }

            if (args.OrderBy != null && args.OrderBy.Length > 0)
            {
                items = items.OrderBy(args.OrderBy);
            }
            else
                items = items.OrderBy(x => x.Name);

            
            
            
            if (args.Name?.Length > 0)
            {
                items = items.Where(x => x.Name.Contains(args.Name));
            }

            int count = items.Count();

            if (args.Skip != null && args.Top != null)
            {
                items = items.Skip(args.Skip.Value).Take(args.Top.Value);
            }
            else
            {
                totalPage = 1;

            }
            bool nextPage = args.PageNumber < totalPage;
            bool previousPage = args.PageNumber > 1;

            var paginationMetadata = new
            {
                totalCount = count,
                pageSize = args.PageSize,
                currentPage = args.PageNumber,
                totalPage = totalPage,
                previousPage = previousPage,
                nextPage = nextPage
            };
            HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

            return await items.ToListAsync();
        }

        

        // GET: api/Products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductParameter>> GetProduct(int id)
        {
            var product = await _context.ProductParameters.FirstOrDefaultAsync(x=>x.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return product;
        }

        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, ProductParameter item)
        {
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
                if (!ProductExists(id))
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

        // POST: api/Products
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPost]
        public async Task<ActionResult<Product>> Post(ProductParameter item)
        {
            _context.ProductParameters.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = item.Id }, item);
        }

        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ProductParameters.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            _context.ProductParameters.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPost("CSV/{idProduct}")]
        public async Task<IActionResult> CSV(IFormFile file, int idProduct)
        {
            try
            {
                var csvRows = file.ReadAsList();


                foreach (var row in csvRows)
                {
                    string[] fields = row.Split(";");
                   
                    if (fields.Length > 1)
                    {
                        var param = await _context.ProductParameters.Where(x=>x.Code == fields[0] && x.IdProduct == idProduct).FirstOrDefaultAsync();

                        if (param == null)
                        {
                            param = new ProductParameter();
                            param.Code = fields[0];
                            param.IdProduct = idProduct;

                            param.Name = fields[1];
                        }
                        
                        param.Code = fields[2];
                        _context.ProductParameters.Add(param);
                    } 
                    
                }
              

                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(CSV), LogEvent.EventsTypes.Error, ex.Message);
                return Content($"Errore: {ex.Message}");
            }
        }

       
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
