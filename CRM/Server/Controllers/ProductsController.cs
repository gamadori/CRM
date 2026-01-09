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
using CRM.Client.Services;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogEventService _logEventService;

        private readonly IPermitsService _permitsService;
        public ProductsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService)
        {
            _context = context;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }


        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] ProductFilter args)
        {
            int totalPage = 0;

            var products =_context.Products.Include(x=>x.ProductType).AsQueryable();

            if (args.Filter != null && args.Filter.Length > 0)
            {
                products = products.Where(args.Filter);
            }

            if (args.OrderBy != null && args.OrderBy.Length > 0)
            {
                products = products.OrderBy(args.OrderBy);
            }
            else
                products = products.OrderBy(x => x.Name);

            if (await _permitsService.IsClient())
            {
                int? idCompany = await _permitsService.GetIdCompany();
                products = products.Where(x => x.Articles.Where(y => y.IdCompany == idCompany).Any());
            }

            if (args.IdParent != null)
            {
                products = products.Where(x => x.Parents.Where(x=>x.Id == args.IdParent).Any());
            }

            
            if (args.Name?.Length > 0)
            {
                products = products.Where(x => x.Name.Contains(args.Name));
            }

            int count = products.Count();

            if (args.Skip != null && args.Top != null)
            {
                products = products.Skip(args.Skip.Value).Take(args.Top.Value);
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

            return await products.ToListAsync();
        }

        

        // GET: api/Products/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetProduct(int id)
        {
            var product = await _context.Products.Include(x=>x.ProductType).FirstOrDefaultAsync(x=>x.Id == id);

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
        public async Task<IActionResult> PutProduct(int id, Product product)
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
        public async Task<ActionResult<Product>> PostProduct(Product product)
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }

        // DELETE: api/Products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPost("CSV/{parent}")]
        public async Task<IActionResult> CSV(string parent, List<string[]> csvRows)
        {
            try
            {
                List<CSVMapping> mappings = await _context.CSVMappings.Where(x => x.TableName == CSVTable.Category.ToString()).ToListAsync();



                foreach (var row in csvRows)
                {
                    var code = CSVHelper.CSVGetField(row, mappings, nameof(Product.Code));

                    if (code != null && code.Length > 0)
                    {
                        var product = _context.Products.FirstOrDefault(x => x.Code == code);
                        if (product == null)
                        {
                            product = new Product();
                            product.Code = code;
                            _context.Products.Add(product);


                            var productType = _context.ProductTypes.Where(x => x.Name == parent).FirstOrDefault();

                            if (productType != null)
                            {
                                product.IdProductType = productType.Id;
                            }

                            product.Name = code;
                            product.Description = code;

                            await _context.SaveChangesAsync();
                        }
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
