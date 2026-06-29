using CNM.Authorize;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ArticlesController : ControllerBase
    {
        private readonly IArticlesService _articlesService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        private readonly ApplicationDbContext _context;

        public ArticlesController(IArticlesService articlesService, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService, ILanguagesService languagesService, ApplicationDbContext context)
        {
            _articlesService = articlesService;
            _userManager = userManager;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _languagesService = languagesService;
            _context = context;
        }


        [HttpGet]
        public async Task<PagingResponse<ArticleDTO>?> GetPage([FromQuery] ArticleFilter? args = null)
        {
            try
            {
                var items = await _articlesService.GetPagingAsync(args);
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<ArticleDTO?>> GetItem(int id)
        {
            try
            {
                var item = await _articlesService.GetItemAsync(id);
                if (item == null)
                {
                    return NotFound();
                }
                return Ok(item);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetItem), LogEvent.EventsTypes.Error, ex);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
        [HttpGet("list")]
        public async Task<IEnumerable<ArticleDTO>?> GetItems([FromQuery] ArticleFilter? args = null)
        {
            try
            {
                var items = await _articlesService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<ArticleDTO>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<ArticleDTO>();
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<ArticleDTO>>> Put(int id, Article item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _articlesService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving article");

            return Ok(resp);
        }

        [HttpPost]
        public async Task<ActionResult<APIResponseMessage<ArticleDTO>>> Post(Article item)
        {
            var resp = await _articlesService.PostAsync(item);

            if (resp == null)
                return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

            return Ok(resp);
        }

        
        // DELETE: api/Products/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var resp = await _articlesService.DeleteAsync(id);

            if (!resp)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Error on deleting Article");
            }
            else
                return NoContent();
        }

        [HttpPost("import-excel")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [Obsolete("Sostituito da ProductsController.ImportExcel")]
        public async Task<ActionResult<ArticleImportResult>> ImportExcel([FromForm] IFormFile file, [FromForm] bool deleteExisting = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File mancante.");

            var result = new ArticleImportResult();
            try
            {
                if (deleteExisting)
                {
                    _context.Articles.RemoveRange(_context.Articles);
                    await _context.SaveChangesAsync();
                }

                using var stream = file.OpenReadStream();
                using var doc = SpreadsheetDocument.Open(stream, false);

                var workbookPart = doc.WorkbookPart!;
                var sheets = workbookPart.Workbook.Sheets!.Elements<Sheet>();
                var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

                // Estrae solo le lettere dalla CellReference (es. "AB12" -> "AB")
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
                    return GetCellValue(cell);
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
                    // Skip riga 1 (intestazione)
                    var rows = wsPart.Worksheet.Descendants<Row>().Where(r => r.RowIndex?.Value > 1);

                    foreach (var row in rows)
                    {
                        // Colonna A = Codice articolo (SerialNumber)
                        var serialNumber = GetColValue(row, "A");
                        // Colonna B = Descrizione (Product.Name)
                        var productName = GetColValue(row, "B");
                        // Colonna V = Creato il (SaleDate, valore OADate numerico)
                        var saleDateRaw = GetColValue(row, "V");

                        if (string.IsNullOrWhiteSpace(serialNumber))
                            continue;

                        if (string.IsNullOrWhiteSpace(productName))
                        {
                            result.ArticlesSkipped++;
                            result.Errors.Add($"Riga {row.RowIndex}: SerialNumber='{serialNumber}' senza nome prodotto.");
                            continue;
                        }

                        var product = await _context.Products.FirstOrDefaultAsync(p => p.Name == productName && p.IdProductType == productType.Id);
                        if (product == null)
                        {
                            product = new Product { Name = productName, IdProductType = productType.Id };
                            _context.Products.Add(product);
                            await _context.SaveChangesAsync();
                            result.ProductsCreated++;
                        }

                        DateTime? saleDate = null;
                        if (!string.IsNullOrWhiteSpace(saleDateRaw) &&
                            double.TryParse(saleDateRaw, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double oaDate))
                        {
                            saleDate = DateTime.FromOADate(oaDate);
                        }

                        _context.Articles.Add(new Article
                        {
                            SerialNumber = serialNumber,
                            IdProduct = product.Id,
                            SaleDate = saleDate,
                        });
                        result.ArticlesCreated++;
                    }

                    await _context.SaveChangesAsync();
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(ImportExcel), LogEvent.EventsTypes.Error, ex);
                result.Success = false;
                result.Errors.Add($"Errore durante l'importazione: {ex.Message}");
            }

            return Ok(result);
        }

        //// Aggiungi questi metodi al controller Articles esistente

        //[HttpGet("{id}/DomainStates")]
        //public async Task<ActionResult<List<ArticleDomainState>>> GetArticleDomainStates(int id)
        //{
        //    var states = await _context.ArticleDomainStates
        //        .Include(ads => ads.Domain)
        //        .Include(ads => ads.CurrentState)
        //        .Include(ads => ads.LastEvent)
        //        .Where(ads => ads.ArticleId == id)
        //        .ToListAsync();

        //    return states;
        //}

        //[HttpGet("{id}/Events")]
        //public async Task<ActionResult<List<ArticleEvent>>> GetArticleEvents(int id, [FromQuery] int? limit = null)
        //{
        //    var query = _context.ArticleEvents
        //        .Include(e => e.Domain)
        //        .Include(e => e.EventType)
        //        .Include(e => e.FromState)
        //        .Include(e => e.ToState)
        //        .Where(e => e.ArticleId == id)
        //        .OrderByDescending(e => e.OccurredAt);

        //    var events = limit.HasValue
        //        ? await query.Take(limit.Value).ToListAsync()
        //        : await query.ToListAsync();

        //    return events;
        //}

        //[HttpPost("{id}/InitializeDomain/{domainId}")]
        //public async Task<IActionResult> InitializeArticleDomain(int id, int domainId)
        //{
        //    // Verifica che l'articolo esista
        //    var article = await _context.Articles.FindAsync(id);
        //    if (article == null) return NotFound();

        //    // Verifica che il dominio esista
        //    var domain = await _context.ArticleDomains.FindAsync(domainId);
        //    if (domain == null) return NotFound("Dominio non trovato");

        //    // Verifica se già esiste uno stato per questo dominio
        //    var existingState = await _context.ArticleDomainStates
        //        .FirstOrDefaultAsync(ads => ads.ArticleId == id && ads.DomainId == domainId);

        //    if (existingState != null)
        //        return BadRequest("Lo stato per questo dominio è già stato inizializzato");

        //    // Trova lo stato iniziale per questo dominio (quello con SortOrder più basso)
        //    var initialState = await _context.ArticleStates
        //        .Where(s => s.DomainId == domainId && s.IsActive)
        //        .OrderBy(s => s.SortOrder)
        //        .FirstOrDefaultAsync();

        //    if (initialState == null)
        //        return BadRequest("Nessuno stato iniziale trovato per questo dominio");

        //    // Crea il domain state
        //    var domainState = new ArticleDomainState
        //    {
        //        ArticleId = id,
        //        DomainId = domainId,
        //        CurrentStateId = initialState.Id,
        //        UpdatedAt = DateTime.UtcNow
        //    };

        //    _context.ArticleDomainStates.Add(domainState);
        //    await _context.SaveChangesAsync();

        //    return Ok(domainState);
        //}
       


    }
}
