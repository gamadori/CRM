using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using TL;

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

        private readonly IProductsService _productsService;

        public ProductsController(ApplicationDbContext context, ILogEventService logEventService, IPermitsService permitsService, IProductsService productsService )
        {
            _context = context;
            _logEventService = logEventService;
            _permitsService = permitsService;
            _productsService = productsService;
        }


        // GET: api/Products
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<Product>>> GetProducts([FromQuery] ProductFilter args)
        //{
        //    int totalPage = 0;

        //    var products =_context.Products.Include(x=>x.ProductType).AsQueryable();

        //    if (args.Filter != null && args.Filter.Length > 0)
        //    {
        //        products = products.Where(args.Filter);
        //    }

        //    if (args.OrderBy != null && args.OrderBy.Length > 0)
        //    {
        //        products = products.OrderBy(args.OrderBy);
        //    }
        //    else
        //        products = products.OrderBy(x => x.Name);

        //    if (await _permitsService.IsClient())
        //    {
        //        int? idCompany = await _permitsService.GetIdCompany();
        //        products = products.Where(x => x.Articles.Where(y => y.IdCompany == idCompany).Any());
        //    }

        //    if (args.IdParent != null)
        //    {
        //        products = products.Where(x => x.Parents.Where(x=>x.Id == args.IdParent).Any());
        //    }


        //    if (args.Name?.Length > 0)
        //    {
        //        products = products.Where(x => x.Name.Contains(args.Name));
        //    }

        //    int count = products.Count();

        //    if (args.Skip != null && args.Top != null)
        //    {
        //        products = products.Skip(args.Skip.Value).Take(args.Top.Value);
        //    }
        //    else
        //    {
        //        totalPage = 1;

        //    }
        //    bool nextPage = args.PageNumber < totalPage;
        //    bool previousPage = args.PageNumber > 1;

        //    var paginationMetadata = new
        //    {
        //        totalCount = count,
        //        pageSize = args.PageSize,
        //        currentPage = args.PageNumber,
        //        totalPage = totalPage,
        //        previousPage = previousPage,
        //        nextPage = nextPage
        //    };
        //    HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));

        //    return await products.ToListAsync();
        //}

        [HttpGet]
        public async Task<PagingResponse<ProductDTO>?> GetPage([FromQuery] ProductFilter? args = null)
        {
            try
            {
                var items = await _productsService.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<ProductDTO>?> GetItems([FromQuery] ProductFilter? args = null)
        {
            try
            {
                var items = await _productsService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<ProductDTO >();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<ProductDTO>();
            }
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _productsService.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ProductDTO>>> Put(int id, Product item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _productsService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving product");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ProductDTO>>> Post(Product item)
        {
            var resp = await _productsService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }


        //// GET: api/Products/5
        //[HttpGet("{id}")]
        //public async Task<ActionResult<Product>> GetProduct(int id)
        //{
        //    var product = await _context.Products.Include(x=>x.ProductType).FirstOrDefaultAsync(x=>x.Id == id);

        //    if (product == null)
        //    {
        //        return NotFound();
        //    }

        //    return product;
        //}

        // PUT: api/Products/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        //[AuthorizeRole(ePolicy.SuperUserRole)]
        //[HttpPut("{id}")]
        //public async Task<IActionResult> PutProduct(int id, Product product)
        //{
        //    if (id != product.Id)
        //    {
        //        return BadRequest();
        //    }

        //    _context.Entry(product).State = EntityState.Modified;

        //    try
        //    {
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        if (!ProductExists(id))
        //        {
        //            return NotFound();
        //        }
        //        else
        //        {
        //            throw;
        //        }
        //    }

        //    return NoContent();
        //}

        //// POST: api/Products
        //// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        //[AuthorizeRole(ePolicy.SuperUserRole)]
        //[HttpPost]
        //public async Task<ActionResult<Product>> PostProduct(Product product)
        //{
        //    _context.Products.Add(product);
        //    await _context.SaveChangesAsync();

        //    return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        //}

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _productsService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Product");
            }
            else
                return NoContent();
        }

       
        // ── Import Excel ────────────────────────────────────────────────────

        [HttpPost("import-excel")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<ActionResult<ProductImportResult>> ImportExcel(
            [FromForm] IFormFile file,
            [FromForm] bool deleteAll = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File mancante.");

            var result = new ProductImportResult();

            try
            {
                if (deleteAll)
                    await PurgeAllProductData();

                using var stream = file.OpenReadStream();
                using var doc = SpreadsheetDocument.Open(stream, false);

                var workbookPart = doc.WorkbookPart!;
                var sheets = workbookPart.Workbook.Sheets!.Elements<Sheet>();
                var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

                static string ColLetter(string? cellRef) =>
                    cellRef == null ? string.Empty : new string(cellRef.TakeWhile(char.IsLetter).ToArray());

                string GetCellValue(Cell? cell)
                {
                    if (cell?.CellValue == null) return string.Empty;
                    var val = cell.CellValue.InnerText;
                    if (cell.DataType?.Value == CellValues.SharedString && sharedStrings != null)
                        return sharedStrings.ElementAt(int.Parse(val)).InnerText;
                    return val;
                }

                string GetColValue(Row row, string col)
                {
                    var cell = row.Elements<Cell>().FirstOrDefault(c => ColLetter(c.CellReference?.Value) == col);
                    return GetCellValue(cell).Trim();
                }

                foreach (var sheet in sheets)
                {
                    var sheetName = (sheet.Name?.Value ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(sheetName)) continue;

                    var productType = await _context.ProductTypes.FirstOrDefaultAsync(pt => pt.Name == sheetName);
                    if (productType == null)
                    {
                        productType = new ProductType { Name = sheetName };
                        _context.ProductTypes.Add(productType);
                        await _context.SaveChangesAsync();
                        result.ProductTypesCreated++;
                    }

                    var wsPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
                    // riga 1 = intestazione, partiamo da riga 2
                    var rows = wsPart.Worksheet.Descendants<Row>().Where(r => r.RowIndex?.Value > 1);

                    foreach (var row in rows)
                    {
                        var code = GetColValue(row, "A");        // Codice articolo → Product.Code
                        var name = GetColValue(row, "B");        // Descrizione → Product.Name
                        var description = GetColValue(row, "C"); // Descrizione supplementare → Product.Description

                        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
                            continue;

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            result.RowsSkipped++;
                            result.Errors.Add($"Foglio '{sheetName}' riga {row.RowIndex}: codice '{code}' senza Descrizione, saltato.");
                            continue;
                        }

                        // Cerca per codice se presente, altrimenti per nome (stesso ProductType)
                        Product? product = null;
                        if (!string.IsNullOrWhiteSpace(code))
                            product = await _context.Products.FirstOrDefaultAsync(p =>
                                p.Code == code && p.IdProductType == productType.Id);

                        if (product == null)
                            product = await _context.Products.FirstOrDefaultAsync(p =>
                                p.Name == name && p.IdProductType == productType.Id);

                        if (product == null)
                        {
                            _context.Products.Add(new Product
                            {
                                Code = code,
                                Name = name,
                                Description = description,
                                IdProductType = productType.Id
                            });
                            result.ProductsCreated++;
                        }
                        else
                        {
                            // Aggiorna i dati se il prodotto esiste già
                            product.Code = code;
                            product.Name = name;
                            product.Description = description;
                            result.ProductsUpdated++;
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ProductsController), nameof(ImportExcel), LogEvent.EventsTypes.Error, ex);
                result.Success = false;
                result.Errors.Add($"Errore durante l'importazione: {ex.Message}");
            }

            return Ok(result);
        }

        private async Task PurgeAllProductData()
        {
            // 1. Annulla i riferimenti nullable verso Articles e Products
            await _context.Tickets.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IdArticle, (int?)null)
                .SetProperty(t => t.IdProduct, (int?)null));

            await _context.TicketInterventionArticles.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IdArticle, (int?)null)
                .SetProperty(t => t.IdProduct, (int?)null));

            await _context.MachineBackups.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IdArticle, (int?)null)
                .SetProperty(m => m.IdProduct, (int?)null));

            await _context.Projects.ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IdProduct, (int?)null));

            // 2. Elimina dipendenti non-nullable di Articles
            await _context.ArticleLicenseFeatures.ExecuteDeleteAsync();
            await _context.ArticleLicenses.ExecuteDeleteAsync();
            await _context.ArticleAccessory.ExecuteDeleteAsync();
            await _context.ArticleEvents.ExecuteDeleteAsync();
            await _context.ArticleDomainStates.ExecuteDeleteAsync();

            // 3. Elimina Articles
            await _context.Articles.ExecuteDeleteAsync();

            // 4. Elimina dipendenti non-nullable di Products
            await _context.ProductCatalogAssets.ExecuteDeleteAsync();

            // Feature defs legate a ProductType/Product specifici
            await _context.ArticleLicenseFeatureDefs
                .Where(d => d.IdProduct != null || d.IdProductType != null)
                .ExecuteDeleteAsync();

            // 5. Elimina Products
            await _context.Products.ExecuteDeleteAsync();

            // 6. Elimina ProductTypes
            await _context.ProductTypes.ExecuteDeleteAsync();
        }
    }
}
