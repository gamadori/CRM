using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Linq.Dynamic.Core;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EmailsSentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly ILogEventService _logEventService;

        public EmailsSentController(ApplicationDbContext context, IPermitsService permitsService, ILogEventService logEventService)
        {
            _context = context;
            _permitsService = permitsService;
            _logEventService = logEventService;
        }

        // GET: api/Companies
        [HttpGet]
        public async Task<IEnumerable<EmailSent?>?> GetEmails([FromQuery] EmailSentFilterModel? args = null)
        {
            try
            {
                int totalPage = 1;
                int? idCompany;

                var user = await _permitsService.GetUser();

                var emails = _context.EmailsSent.AsQueryable();

                if (args?.OrderBy != null && args.OrderBy.Length > 0)
                {
                    emails = emails.OrderBy(args.OrderBy);
                }
                else
                    emails = emails.OrderBy(x => x.DateSent);

                if (args?.Filter != null && args.Filter.Length > 0)
                    emails = emails.Where(args.Filter);


                emails = emails.Where(x => x.IdUser == user.Id);


               

                int count = emails.Count();

                if (args?.Skip != null && args.Top != null)
                {
                    emails = emails.Skip(args.Skip.Value).Take(args.Top.Value);
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
                // var list = await companies.ToListAsync();
                return await emails.ToListAsync();
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(EmailsSentController), nameof(GetEmails), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }
        // GET: api/Companies/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EmailSent>> GetEmail(int id)
        {
            var email = await _context.EmailsSent.FindAsync(id);

            if (email == null)
            {
                return NotFound();
            }

            //var breadcrumb = GetBreadcrumb(company);

            //HttpContext.Response.Headers.Add(ValuesHelper.BreadcrumbHeader, JsonConvert.SerializeObject(breadcrumb));
            return email;
        }

    }
}
