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
using Microsoft.AspNetCore.Authorization;
using CRM.Server.Services;
using CRM.Shared.Helper;
using CRM.Server.Helpers;
using Microsoft.Extensions.Primitives;
using CNM.Authorize;
using CRM.Client.Shared;
using CRM.Shared.DTOs;



namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CompaniesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public CompaniesController(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

      

        // GET: api/Companies
        [HttpGet]
        public async Task<IEnumerable<Company?>> GetCompany([FromQuery] CompanyFilter? args = null)
        {
            try
            {
                int totalPage = 1;
               

                var companies = await FilterCompany(args);

                if (companies == null)
                {
                    return Enumerable.Empty<Company>();
                }
                int count = companies.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    companies = companies.Skip(args.Skip.Value).Take(args.Top.Value);
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
                    pageSize = args != null ? args.PageSize: 0,
                    currentPage = args != null ? args.PageNumber: 0,
                    totalPage = totalPage,
                    previousPage = previousPage,
                    nextPage = nextPage
                };
                HttpContext.Response.Headers.Add("Paging-Header", JsonConvert.SerializeObject(paginationMetadata));
                
                return await companies.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetCompany), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<Company>();
            }
        }

        [HttpGet("items")]
        public async Task<IEnumerable<CompanyDTO?>> GetitemsCompany([FromQuery] CompanyFilter? args = null)
        {
            try
            {
               

                var companies = await FilterCompany(args);

                if (companies == null)
                {
                    return Enumerable.Empty<CompanyDTO>();
                }

                var items = companies.Select(x => new CompanyDTO() { Id = x.Id, RagioneSociale = x.RagioneSociale });
                
                return await items.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(GetCompany), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<CompanyDTO>();
            }
        }

       

        // GET: api/Companies/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Company>> GetCompany(int id)
        {

            var resp = await _permitsService.CompanyCanAccess(id);

            if (resp == null || !resp.CanAccess || resp.IdCompany == null)
            {
                return NotFound();
            }

            id = (int)resp.IdCompany;

            var company = await _context.Companies.FindAsync(id);

            

            if (company == null)
            {
                return NotFound();
            }

            company.ResellerName = await CompanyName(company.Id);

             
            return company;
        }
        // GET: api/Companies/5
        [HttpGet("user")]
        public async Task<ActionResult<Company>> GetUserCompany()
        {
            var user = await _permitsService.GetUser();

            if (user != null)
            {
                var company = await _context.Companies.FindAsync(user.IdCompany);

                if (company == null)
                {
                    return NotFound();
                }

              
                return company;
            }
            else
                return NotFound("user");
        }

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AuthorizeRole(ePolicy.StandardRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCompany(int id, Company company)
        {
            if (id != company.Id)
            {
                return BadRequest();
            }

            var companyUser = await _permitsService.GetCompany();

            if (companyUser != null)
            {
                if (companyUser.CompanyType == CompanyTypes.Customer && id != companyUser.Id)
                {
                    // Un cliente puo modficare solo la sua azienda
                    return BadRequest();
                }
                else if (companyUser.CompanyType == CompanyTypes.Reseller && id != companyUser.Id && await GetIdReseller(id) != companyUser.Id)
                {
                    // Un rivenditore puo' modificare solo la sua azienda o una dei suoi clienti
                    return BadRequest();
                }
            }
            _context.Entry(company).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanyExists(id))
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

        // POST: api/Companies
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        
        [HttpPost]
        public async Task<ActionResult<Company>> PostCompany(Company company)
        {
            var companyUser = await _permitsService.GetCompany();

            if (companyUser != null)
            {
                if (companyUser.CompanyType == CompanyTypes.Customer || companyUser.CompanyType == CompanyTypes.Reseller && company.IdReseller != companyUser.Id)
                {
                    // Dato non congruo
                    // Un customer non puo' aggiungere un'azionda
                    // Un rivenditore puo' aggiungere solo un suo cliente

                    await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(PostCompany), LogEvent.EventsTypes.Error, "Dato non comgruo");

                    return BadRequest();
                }
                else
                {
                    _context.Companies.Add(company);
                    await _context.SaveChangesAsync();
                }
            }
            return CreatedAtAction("GetCompany", new { id = company.Id }, company);
        }

        

        // DELETE: api/Companies/5
        [AuthorizeRole(ePolicy.AdminRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var company = await _context.Companies.FindAsync(id);
            if (company == null)
            {
                return NotFound();
            }

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("removecustomer")]
        public async Task<IActionResult> RemoveCustomer(CustomerModel item)
        {
            var customer = await _context.Companies.FindAsync(item.IdCustomer);

            if (customer != null && customer.IdReseller == item.IdReseller)
            {
                customer.IdReseller = null;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            else
                return BadRequest();
        }

        [HttpPost("addcustomer")]
        public async Task<IActionResult> AddCustomer(CustomerModel item)
        {
            var customer = await _context.Companies.FindAsync(item.IdCustomer);

            if (customer != null && customer.IdReseller != item.IdReseller)
            {
                customer.IdReseller = item.IdReseller;
                await _context.SaveChangesAsync();
                return NoContent();
            }
            else
                return BadRequest();
        }


        [HttpPost("CSV/{fileName}")]
        public async Task<IActionResult> CSV(string fileName, List<string[]> csvRows)        
        {
            try
            {
                List<CSVMapping> mappings = await _context.CSVMappings.Where(x => x.TableName == nameof(Company)).ToListAsync();

                CSVMapping colInternalCode = mappings.Where(x => x.FieldName == nameof(Company.InternalCode)).FirstOrDefault();

                if (colInternalCode != null)
                {
                    foreach (var row in csvRows)
                    {
                        if (row.Length > colInternalCode.NumCol)
                        {
                            if (int.TryParse(row[colInternalCode.NumCol], out int internalCode))
                            {
                                
                                Company company = await _context.Companies.Where(x => x.InternalCode == internalCode).FirstOrDefaultAsync();

                                if (company == null)
                                {
                                    company = new Company();
                                    company.InternalCode = int.Parse(CSVHelper.CSVGetField(row, mappings, nameof(Company.InternalCode)));
                                    _context.Companies.Add(company);
                                }

                                company.Cap = CSVHelper.CSVGetField(row, mappings, nameof(Company.Cap)) ?? company.Cap;
                                company.Citta = CSVHelper.CSVGetField(row, mappings, nameof(Company.Citta)) ?? company.Citta;
                                company.CodiceFiscale = CSVHelper.CSVGetField(row, mappings, nameof(Company.CodiceFiscale)) ?? company.CodiceFiscale;
                                company.Email = CSVHelper.CSVGetField(row, mappings, nameof(Company.Email)) ?? company.Email;
                                company.Fax = CSVHelper.CSVGetField(row, mappings, nameof(Company.Fax)) ?? company.Fax;
                                company.Indirizzo = CSVHelper.CSVGetField(row, mappings, nameof(Company.Indirizzo)) ?? company.Indirizzo;
                                company.Mobile = CSVHelper.CSVGetField(row, mappings, nameof(Company.Mobile)) ?? company.Mobile;
                                company.PIva = CSVHelper.CSVGetField(row, mappings, nameof(Company.PIva)) ?? company.PIva;
                                company.Provincia = CSVHelper.CSVGetField(row, mappings, nameof(Company.Provincia)) ?? company.Provincia;
                                company.RagioneSociale = CSVHelper.CSVGetField(row, mappings, nameof(Company.RagioneSociale)) ?? company.RagioneSociale;
                                company.Stato = CSVHelper.CSVGetField(row, mappings, nameof(Company.Stato)) ?? company.Stato;
                                company.Telefono = CSVHelper.CSVGetField(row, mappings, nameof(Company.Telefono)) ?? company.Telefono;


                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(CSV), LogEvent.EventsTypes.Error, ex.Message);
                return Content($"Errore: {ex.Message}");
            }
        }

        [HttpGet("emailaddresses/{idCompany}")]
        public async Task<ActionResult<IEnumerable<string>>> GetEmailAddress(int idCompany)
        {
            List<string> emailAddresses = new List<string>();

            var company = await _context.Companies.Include(x=>x.ApplicationUsers).FirstOrDefaultAsync(x=>x.Id == idCompany);

            if (company != null)
            {
                emailAddresses.Add(company.Email);
                
                foreach (var user in company.ApplicationUsers)
                {
                    emailAddresses.Add(user.Email);
                }
            
            }
            return emailAddresses;
        }


       
        private async Task<IQueryable<Company>?> FilterCompany(CompanyFilter? args = null)
        {
            try
            {
                int? idCompany;

                idCompany = await _permitsService.GetIdCompany();

                var companies = _context.Companies.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    companies = companies.OrderBy(args.OrderBy);
                }
                else
                    companies = companies.OrderBy(x => x.RagioneSociale);

                
                if (args?.Filter != null && args.Filter.Any())
                {
                    companies = companies.Where(args.Filter);
                }

                var comapnyType = await _permitsService.CompanyType();
                
                
                switch (comapnyType)
                {
                    case CompanyTypes.Customer:
                        
                        companies = companies.Where(x => x.Id == idCompany);
                        break;

                    case CompanyTypes.Reseller:

                        companies = companies.Where(x=>x.Id == idCompany || x.IdReseller == idCompany);
                        break;

                }

                //if (!await _permitsService.CanAccessOtherCompany())
                //{
                //    idCompany = await _permitsService.GetIdCompany();
                //    companies = companies.Where(x => x.Id == idCompany);
                //}

                if (args?.RagioneSociale != null && args.RagioneSociale.Length > 0)
                {
                    companies = companies.Where(x => x.RagioneSociale.Contains(args.RagioneSociale));
                }

                if (args?.Stato != null && args.Stato.Length > 0)
                {
                    companies = companies.Where(x => x.Stato.Contains(args.Stato));
                }

                if (args?.IdReseller != null && args.IdReseller > 0)
                {
                    companies = companies.Where(x => x.IdReseller == args.IdReseller);
                }

                if (args?.CompanyType != null)
                {
                    companies = companies.Where(x => x.CompanyType == args.CompanyType);
                }    

                if (args?.IdCompanyParent != null)
                {
                    companies = companies.Where(x=>x.IdReseller !=  args.IdCompanyParent && x.CompanyType != CompanyTypes.HeadCompany && x.Id != args.IdCompanyParent);
                }


                if (args != null && args.Reseller)
                {
                    companies = companies.Where(x=>x.CompanyType == CompanyTypes.Reseller || x.CompanyType == CompanyTypes.HeadCompany);
                }
                return companies;

               
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(CompaniesController), nameof(FilterCompany), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }


        private bool CompanyExists(int id)
        {
            return _context.Companies.Any(e => e.Id == id);
        }


        private async Task<string> CompanyName(int id)
        {
            var company = await _context.Companies.FindAsync(id);

            return company?.RagioneSociale ?? "";
        }

        private List<BreadcrumbModel> GetBreadcrumb(Company company)
        {
            List<BreadcrumbModel> bread = new List<BreadcrumbModel>();

            if (company == null)
            {
                return null;
            }

            bread.Add(new BreadcrumbModel() { Title = $"Aziende", Url = $"Companies" });
            bread.Add(new BreadcrumbModel() { Title = $"{company.RagioneSociale}", Url = null });
            
            return bread;
        }


        private async Task<int?> GetIdReseller(int id)
        {
            var company = await _context.Companies.FindAsync(id);



            if (company != null)
            {
                var idReseller = company.IdReseller;

                _context.Entry(company).State = EntityState.Detached;

                return idReseller;
            }
            else
                return null;
        }
        
    }
}
