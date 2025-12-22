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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CRM.Server.Services;
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CNM.Authorize;
using System.Drawing.Printing;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ArticlesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;
        private readonly ILanguagesService _languagesService;
        public ArticlesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermitsService permitsService, ILogEventService logEventService, ILanguagesService languagesService)
        {
            _context = context;
            _userManager = userManager;
            _permitsService = permitsService;
            _logEventService = logEventService;
            _languagesService = languagesService;
        }
      
      
        // GET: api/Products
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticles([FromQuery] ArticleFilter args)
        {
            try
            {
                var articles = _context.Articles.Include(x => x.Company).Include(x => x.Product).AsQueryable();

                
                
                var resp = await _permitsService.CompanyCanAccess(args?.IdCompany);

                if (resp == null || !resp.CanAccess)
                {
                    return new List<Article>();
                }
                else
                {
                    if (resp.IdCompany != null)
                        args.IdCompany  = resp.IdCompany;

                    if (resp.IdCompanies != null && resp.IdCompanies.Any())
                        articles = articles.Where(x => x.IdCompany != null && resp.IdCompanies.Contains((int)x.IdCompany));
                }
                   

                if (args.IdCompany != null)
                    articles = articles.Where(x => x.IdCompany == args.IdCompany);

                if (args.IdProduct != null)
                    articles = articles.Where(x => x.IdProduct == args.IdProduct);

                if (args.SerialNumber != null && args.SerialNumber.Length > 0)
                    articles = articles.Where(x => x.SerialNumber.Contains(args.SerialNumber));

                if (args.Filter != null && args.Filter.Trim().Length > 0)
                {
                    articles = articles.Where(args.Filter);
                }

                if (args.OrderBy != null && args.OrderBy.Length > 0)
                {
                    articles = articles.OrderBy(args.OrderBy);
                }
                else
                    articles = articles.OrderBy(x => x.Product.Name).ThenBy(x=>x.Name);

                int count = articles.Count();
                int totalPage = 0;

                if (args.Skip != null && args.Top != null)
                {
                    articles = articles.Skip(args.Skip.Value).Take(args.Top.Value);
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

                return await articles.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(GetProduct), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }
       

        // GET: api/articles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Article>> GetArticle(int id)
        {
            var article = await _context.Articles.Include(x => x.Company).Include(x=>x.Product).Where(x => x.Id == id).FirstOrDefaultAsync();

            if (article == null) 
            {
                return NotFound();
            }

            var resp = await _permitsService.CompanyCanAccess(article.IdCompany);

            if (resp.CanAccess)
            {

                if (resp.IdCompany != null && article?.IdCompany != resp.IdCompany)
                {
                    article = null;
                }
            }
            if (article == null)
            {
                return NotFound();
            }

             var breadcrumb = GetBreadcrumb(article);

            

            HttpContext.Response.Headers.Add(ValuesHelper.BreadcrumbHeader, JsonConvert.SerializeObject(breadcrumb));

            
            return article;
        }

        // PUT: api/articles/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.SuperUserRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProduct(int id, Article product)
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
        public async Task<ActionResult<Article>?> PostArticle(Article product)
        {
            try
            {
                _context.Articles.Add(product);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetArticle", new { id = product.Id }, product);
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(PostArticle), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        // DELETE: api/Products/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Articles.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            _context.Articles.Remove(product);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpPost("CSV/{parent}")]
        public async Task<IActionResult> CSV(string parent, List<string[]> csvRows)
        {
            bool header = false;
            try
            {
                Article article;
                List<CSVMapping> mappings = await _context.CSVMappings.Where(x => x.TableName == CSVTable.Article.ToString()).ToListAsync();



                foreach (var row in csvRows)
                {
                    if (!header)
                    {
                        header = true;
                    }
                    else
                    {
                        var code = CSVHelper.CSVGetField(row, mappings, nameof(Article.SerialNumber));

                        if (code != null && code.Length > 0)
                        {
                            var items = _context.Articles.Where(x => x.SerialNumber == code);


                            var p = CSVHelper.CSVGetField(row, mappings, nameof(Article.IdProduct));


                            if (items.Count() > 1)
                            {
                                await _logEventService.RegisterAsync(nameof(ArticlesController), nameof(CSV), LogEvent.EventsTypes.Error, "Errore importazione CSV ci sono più articoli con lo stesso serial number");
                            }
                            else
                            {
                                if (items.Any())
                                {
                                    article = await items.FirstAsync();
                                }
                                else
                                {
                                    article = new Article();
                                    article.SerialNumber = code;

                                    _context.Articles.Add(article);
                                }


                                article.IdProduct = await GetProduct(mappings, row);
                                article.IdCompany = await GetCompany(mappings, row);
                                article.Property1 = CSVHelper.CSVGetField(row, mappings, nameof(Article.Property1));

                                await _context.SaveChangesAsync();
                            }

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

        private async Task<int?> GetProduct(List<CSVMapping> mappings, string[] row)
        {
            var code = CSVHelper.CSVGetField(row, mappings, nameof(Article.IdProduct));
            var product = await _context.Products.FirstOrDefaultAsync(x=>x.Code == code);

            return product?.Id;
        }

        

        private async Task<int?> GetCompany(List<CSVMapping> mappings, string[] row)
        {
            var code = CSVHelper.CSVGetField(row, mappings, nameof(Article.IdCompany));

            if (int.TryParse(code, out int value))
            {
                var company = await _context.Companies.FirstOrDefaultAsync(x => x.InternalCode == value);
                return company?.Id;
            }
            else
                return null;
        }

        private bool ProductExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id);
        }

       
        private List<BreadcrumbModel> GetBreadcrumb(Article article)
        {
            List<BreadcrumbModel> bread = new List<BreadcrumbModel>();
            string root = null;
            if (article == null)
            {
                return null;
            }
            var queryString = Request.Query;

            if (queryString.Keys.Contains("$root"))
            {
                root = queryString["$root"];
            }

            if (root == "company")
            {
                bread.Add(new BreadcrumbModel() { Title = $"Home", Url = $"/" });
                bread.Add(new BreadcrumbModel() { Title = $"Aziende", Url = $"Companies" });
                bread.Add(new BreadcrumbModel() { Title = $"{article.Company.RagioneSociale}", Url = $"Companies/{article.Company.Id}" });
                bread.Add(new BreadcrumbModel() { Title = "Articoli", Url = $"Companies/{article.Company.Id}/{(int)CompanyViews.Articles}" });
                bread.Add(new BreadcrumbModel() { Title = $"{article.Product.Name} - {article.SerialNumber}", Url = null });
            }
            else
            {
                bread.Add(new BreadcrumbModel() { Title = "Articoli", Url = $"Articles" });
                bread.Add(new BreadcrumbModel() { Title = $"{article.Product.Name} - {article.SerialNumber}", Url = null });
            }
            return bread;
        }


    }
}
