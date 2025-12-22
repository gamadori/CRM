using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using CRM.Server.Data;
using CRM.Shared;
using Newtonsoft.Json;
using CRM.Shared.Helper;
using CRM.Server.Services;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;

        public ProductsTypesController(ApplicationDbContext context, ILogEventService logEventService)
        {
            _context = context;
            _logEventService = logEventService;
        }

        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductType>>> GetProductsTypes([FromQuery] ProductTypeFilter args)
        {
            try
            {
                int totalPage = 1;

                var productType = _context.ProductTypes.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    productType = productType.OrderBy(args.OrderBy);
                }
                else
                    productType = productType.OrderBy(x => x.Name);

                if (args?.Filter != null && args.Filter.Any())
                {
                    productType = productType.Where(args.Filter);
                }

                int count = productType.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    productType = productType.Skip(args.Skip.Value).Take(args.Top.Value);
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
             
                return await productType.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(GetProductsTypes), LogEvent.EventsTypes.Error, ex);
                return Problem(ex.Message);
            }
        }

        // GET: api/Products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductType>> GetProductType(int id)
        {
            var productType = await _context.ProductTypes.FindAsync(id);

            if (productType == null)
            {
                return NotFound();
            }

            return productType;
        }

        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProductType(int id, ProductType product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            _context.Entry(product).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductTypeExists(id))
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
        [HttpPost]
        public async Task<ActionResult<ProductType>> PostProduct(ProductType productType)
        {
            _context.ProductTypes.Add(productType);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductType), new { id = productType.Id }, productType);
        }

        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductType(int id)
        {
            var productType = await _context.ProductTypes.FindAsync(id);
            if (productType == null)
            {
                return NotFound();
            }

            _context.ProductTypes.Remove(productType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("CSV/{parent}")]
        public async Task<IActionResult> CSV(string parent, List<string[]> csvRows)
        {
            try
            {
                List<CSVMapping> mappings = await _context.CSVMappings.Where(x => x.TableName == CSVTable.Category.ToString()).ToListAsync();

                
                 
                foreach (var row in csvRows)
                {
                    var name = CSVHelper.CSVGetField(row, mappings, nameof(ProductType.Name));

                    if (name != null && name.Length > 0)
                    {
                        var productType = _context.ProductTypes.Where(x => x.Name == name).FirstOrDefault();

                        if (productType == null)
                        {
                            productType = new ProductType();
                            productType.Name = name;
                            _context.ProductTypes.Add(productType);
                        }
                        productType.Description = CSVHelper.CSVGetField(row, mappings, nameof(ProductType.Description)) ?? productType.Description;

                    }

                }
                await _context.SaveChangesAsync();
                
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(CSV), LogEvent.EventsTypes.Error, ex.Message);
                return Content($"Errore: {ex.Message}");
            }
        }

        
        private async Task<ProductType> GetProductType(string name, string description)
        {
            try
            {
                var productType = _context.ProductTypes.Where(x => x.Name == name).FirstOrDefault();

                if (productType == null)
                {
                    productType = new ProductType();
                    productType.Name = name;

                    _context.ProductTypes.Add(productType);

                    
                }
                productType.Description = description;
                await _context.SaveChangesAsync();

                return productType;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsTypesController), nameof(GetProductType), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }
        private bool ProductTypeExists(int id)
        {
            return _context.ProductTypes.Any(e => e.Id == id);
        }
    }
}
