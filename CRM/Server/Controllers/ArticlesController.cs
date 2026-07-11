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
using System.Globalization;
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
        [AuthorizeRole(ePolicy.AdminRole)]
        public async Task<ActionResult<ArticleImportResult>> ImportExcel([FromForm] IFormFile file, [FromForm] bool deleteExisting = false)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File mancante.");

            var result = new ArticleImportResult();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingArticles = await _context.Articles.ToListAsync();
                if (deleteExisting)
                {
                    _context.Articles.RemoveRange(existingArticles);
                    existingArticles.Clear();
                }

                var productsByCode = (await _context.Products
                        .AsNoTracking()
                        .Where(p => p.Code != null && p.Code != "")
                        .ToListAsync())
                    .GroupBy(p => p.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                static string CompanyKey(string name, string country) =>
                    $"{name.Trim()}\u001F{country.Trim()}";

                var companiesByNameAndCountry = (await _context.Companies.ToListAsync())
                    .Where(c => !string.IsNullOrWhiteSpace(c.RagioneSociale))
                    .GroupBy(
                        c => CompanyKey(c.RagioneSociale, c.Stato ?? string.Empty),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var serialNumbers = new HashSet<string>(
                    existingArticles
                        .Where(a => !string.IsNullOrWhiteSpace(a.SerialNumber))
                        .Select(a => a.SerialNumber.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                using var stream = file.OpenReadStream();
                using var doc = SpreadsheetDocument.Open(stream, false);

                var workbookPart = doc.WorkbookPart
                    ?? throw new InvalidOperationException("Il file non contiene una cartella di lavoro Excel valida.");
                var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToList()
                    ?? new List<Sheet>();
                var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

                static string ColLetter(string? cellRef) =>
                    cellRef == null ? string.Empty : new string(cellRef.TakeWhile(char.IsLetter).ToArray());

                string GetCellValue(Cell? cell)
                {
                    if (cell == null) return string.Empty;

                    if (cell.DataType?.Value == CellValues.InlineString)
                        return cell.InlineString?.InnerText?.Trim() ?? string.Empty;

                    var val = cell.CellValue?.InnerText ?? string.Empty;
                    if (cell.DataType?.Value == CellValues.SharedString && sharedStrings != null)
                    {
                        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                            ? sharedStrings.ElementAt(index).InnerText.Trim()
                            : string.Empty;
                    }

                    return val.Trim();
                }

                string GetColValue(Row row, string col)
                {
                    var cell = row.Elements<Cell>().FirstOrDefault(c => ColLetter(c.CellReference?.Value) == col);
                    return GetCellValue(cell);
                }

                static string NormalizeSerialNumber(string value)
                {
                    var separator = value.LastIndexOf('-');
                    return (separator >= 0 ? value[(separator + 1)..] : value).Trim();
                }

                static bool IsKnownCompany(string value) =>
                    !string.IsNullOrWhiteSpace(value) &&
                    !string.Equals(value.Trim(), "???", StringComparison.OrdinalIgnoreCase);

                Company? GetOrCreateCompany(string name, string country)
                {
                    name = name.Trim();
                    country = country.Trim();
                    if (!IsKnownCompany(name))
                        return null;

                    var key = CompanyKey(name, country);
                    if (companiesByNameAndCountry.TryGetValue(key, out var company))
                        return company;

                    company = new Company
                    {
                        RagioneSociale = name,
                        Stato = string.IsNullOrWhiteSpace(country) ? null : country,
                        CompanyType = CompanyTypes.Customer
                    };
                    _context.Companies.Add(company);
                    companiesByNameAndCountry.Add(key, company);
                    result.CompaniesCreated++;
                    return company;
                }

                foreach (var sheet in sheets)
                {
                    var sheetName = (sheet.Name?.Value ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(sheetName)) continue;

                    if (sheet.Id?.Value == null)
                        continue;

                    result.SheetsProcessed++;
                    var wsPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);
                    var rows = wsPart.Worksheet.Descendants<Row>().Where(r => r.RowIndex?.Value > 1);

                    foreach (var row in rows)
                    {
                        var yearRaw = GetColValue(row, "A");
                        var productCode = GetColValue(row, "B");
                        var serialNumberRaw = GetColValue(row, "C");
                        var customerName = GetColValue(row, "D");
                        var customerCountry = GetColValue(row, "E");
                        var recipientName = GetColValue(row, "F");
                        var recipientCountry = GetColValue(row, "G");

                        if (string.IsNullOrWhiteSpace(yearRaw) &&
                            string.IsNullOrWhiteSpace(productCode) &&
                            string.IsNullOrWhiteSpace(serialNumberRaw))
                            continue;

                        result.RowsProcessed++;
                        var rowNumber = row.RowIndex?.Value ?? 0;

                        if (!int.TryParse(yearRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
                        {
                            result.ArticlesSkipped++;
                            result.Errors.Add($"Foglio '{sheetName}', riga {rowNumber}: anno '{yearRaw}' non valido.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(productCode) ||
                            !productsByCode.TryGetValue(productCode, out var product))
                        {
                            result.ArticlesSkipped++;
                            result.Errors.Add($"Foglio '{sheetName}', riga {rowNumber}: prodotto con codice '{productCode}' non trovato.");
                            continue;
                        }

                        var serialNumber = NormalizeSerialNumber(serialNumberRaw);
                        if (string.IsNullOrWhiteSpace(serialNumber))
                        {
                            result.ArticlesSkipped++;
                            result.Errors.Add($"Foglio '{sheetName}', riga {rowNumber}: matricola mancante.");
                            continue;
                        }

                        if (!serialNumbers.Add(serialNumber))
                        {
                            result.ArticlesSkipped++;
                            result.Errors.Add($"Foglio '{sheetName}', riga {rowNumber}: matricola '{serialNumber}' già presente.");
                            continue;
                        }

                        var customer = GetOrCreateCompany(customerName, customerCountry);
                        var recipient = GetOrCreateCompany(recipientName, recipientCountry);

                        _context.Articles.Add(new Article
                        {
                            Year = year,
                            SerialNumber = serialNumber,
                            IdProduct = product.Id,
                            IdCompany = customer?.Id > 0 ? customer.Id : null,
                            Company = customer,
                            Country = string.IsNullOrWhiteSpace(customerCountry) ? null : customerCountry.Trim(),
                            IdRecipientCompany = recipient?.Id > 0 ? recipient.Id : null,
                            RecipientCompany = recipient,
                            RecipientCountry = string.IsNullOrWhiteSpace(recipientCountry) ? null : recipientCountry.Trim()
                        });
                        result.ArticlesCreated++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                result.Success = true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
