using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Server.Data;
using CRM.Shared;
using CRM.Server.Services;
using Newtonsoft.Json;
using CRM.Server.Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Linq.Dynamic.Core;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Data;
using CRM.Shared.Extensions;
using SelectPdf;
using CRM.Client.Helpers;
using CRM.Server.Extensions;
using CRM.Shared.Resources.Models;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketInterventionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermitsService _permitsService;
        private readonly IArchiveService _archiveService;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly ILogEventService _logEventService;
        private readonly IEmailSenderPlus _emailSender;
        public TicketInterventionsController(ApplicationDbContext context, IPermitsService permitsService, IArchiveService archiveService,
            IWebHostEnvironment hostEnvironment, ILogEventService logEventService, IEmailSenderPlus emailSender)
        {
            _context = context;
            _permitsService = permitsService;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Interventions;
            _hostEnvironment = hostEnvironment;
            _logEventService = logEventService;
            _emailSender = emailSender;
        }

        // GET: api/TicketInterventions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TicketIntervention>>> GetTicketIntervention([FromQuery] TicketInterventionFilter? args = null)
        {
            try
            {
                var items = _context.TicketsInterventions.Include(x => x.TicketInterventionsTypes).AsQueryable();

                if (!await _permitsService.CanAccessOtherCompany())
                {
                    int? idCompany = await _permitsService.GetIdCompany();
                    items = items.Where(x => x.Ticket.IdCompany == idCompany);

                }
                if (args != null)
                {
                    if (args.OrderBy != null)
                        items = items.OrderBy(args.OrderBy);

                    if (args.IdTicket != null)
                        items = items.Where(x => x.IdTicket == args.IdTicket);

                    if (args.Filter != null)
                        items = items.Where(args.Filter);

                    
                    PagingHelper.ResponsePaging<TicketIntervention, TicketInterventionFilter>(HttpContext, items, args);

                    
                }
                var list = await items.ToListAsync();

                foreach (var item in list)
                {
                    int? idCompany = await TicketGetIdCompany(item.IdTicket);

                    if (idCompany != null)
                    {
                        item.Permits = await _permitsService.ObjectPermits(idCompany, item.IdUser);
                        item.UserName = (await _context.Users.FindAsync(item.IdUser))?.UserName;
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(GetTicketIntervention), LogEvent.EventsTypes.Error, ex);
                return new List<TicketIntervention>();
            }
        }

        // GET: api/TicketInterventions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketIntervention>> GetTicketIntervention(int id)
        {



            var ticketIntervention = await _context.TicketsInterventions.Include(x=>x.TicketInterventionsTypes).Where(x => x.Id == id).FirstOrDefaultAsync();

            if (ticketIntervention == null)
            {
                return NotFound();
            }
            else if (!await _permitsService.CanGetObject(await TicketGetIdCompany(ticketIntervention.IdTicket)))
            {

                return BadRequest();
            }

            foreach (var item in ticketIntervention.TicketInterventionsTypes)
            {
                ticketIntervention.InterventionsTypesId.Add(item.Id);
            }

            ticketIntervention.AttachmentExist = ExistReport(id);
            ticketIntervention.Permits = await _permitsService.ObjectPermits(ticketIntervention.Ticket.IdCompany, ticketIntervention.IdUser);


            ticketIntervention.InterventionArticles = await _context.TicketInterventionArticles.Where(x=>x.IdTicketIntervention == id).Select(x=> new TicketInterventionArticleModel() {Id = Guid.NewGuid(), IdArticle = x.IdArticle, IdProduct = x.IdProduct, 
                Description = x.Description, IdTicketIntervention = x.IdTicketIntervention, IdLink = x.Id, Product =  x.Product.Name, Article =  x.Article.SerialNumber
            }).ToListAsync();

            return ticketIntervention;
        }

        [HttpGet("CompanyEmailAdresses/{Id}")]
        public async Task<ActionResult<List<string>>> GetCompanyAddressEmail(int id)
        {
            List<string> emailAddresses = new List<string>();

            var ticket = await _context.Tickets.Where(x => x.TicketInterventions.Where(x => x.Id == id).Any()).FirstOrDefaultAsync();

            if (ticket != null)
            {
                var company = await _context.Companies.Include(x => x.ApplicationUsers).FirstOrDefaultAsync(x => x.Id == ticket.IdCompany);

                if (company != null)
                {
                    if (company.Email != null && company.Email.Length > 0)
                        emailAddresses.Add(company.Email);

                    foreach (var user in company.ApplicationUsers)
                    {
                        emailAddresses.Add(user.Email);
                    }

                }

            }
            return emailAddresses;
        }
        // PUT: api/TicketInterventions/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTicketIntervention(int id, TicketIntervention ticketIntervention)
        {
            if (id != ticketIntervention.Id)
            {
                return BadRequest();
            }

            

            _context.Entry(ticketIntervention).State = EntityState.Modified;

            await UpdateArticles(ticketIntervention);

            try
            {
                

              
                
                await _context.SaveChangesAsync();

                await InterventionType(id, ticketIntervention.InterventionsTypesId);
             
                
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketInterventionExists(id))
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


        // POST: api/TicketInterventions
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult> PostTicketIntervention(TicketIntervention ticketIntervention)
        {
            try               
            {

                
                _context.TicketsInterventions.Add(ticketIntervention);
                await _context.SaveChangesAsync();
                await InterventionType(ticketIntervention.Id, ticketIntervention.InterventionsTypesId);
                await UpdateArticles(ticketIntervention);

                return CreatedAtAction("GetTicketIntervention", new { id = ticketIntervention.Id }, ticketIntervention);
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(PostTicketIntervention), LogEvent.EventsTypes.Error, ex);
                return NoContent();
            }
        }

        // DELETE: api/TicketInterventions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketIntervention(int id)
        {
            var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
            if (ticketIntervention == null)
            {
                return NotFound();
            }

            _context.TicketsInterventions.Remove(ticketIntervention);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("Report/{id}")]
        public async Task<bool> CreateDocPdf(int id)
        {

           

            
                if ((await CreatePdf(id)) != null)
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    ticketIntervention.HasAttachments = true;
                    _context.Entry(ticketIntervention).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return true;
                }
                else
                    return false;
            


        }

        [HttpPost("UploadReport/{id}")]
        public async Task<bool> PostReport(int id, UploadFilesModel item)
        {
            try
            {
                var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                var file = item.Files.FirstOrDefault();

                if (ticketIntervention != null && file != null)
                {
                    string path = _archiveService.GetPath(id, "pdf");
                    _archiveService.SaveAttachments(id, file.ContentType, file.Content);

                    _context.Entry(ticketIntervention).State = EntityState.Modified;
                    ticketIntervention.HasAttachments = true;

                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(PostReport), LogEvent.EventsTypes.Error, ex.Message);
                return false;
            }
        }

        [HttpGet("Download/{id}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            try
            {
                if (await _permitsService.CanDownloadInterventionReport(id))
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    if (ticketIntervention != null && ticketIntervention.HasAttachments)
                    {
                        byte[] bytes = _archiveService.GetAttachment(id, "pdf");

                        AttachmentResponse header = new AttachmentResponse();
                        var name = $"{id}.pdf";
                        header.ContentType = MimeKit.MimeTypes.GetMimeType(name);
                        header.Name = name;
                        HttpContext.Response.Headers.Add(ConstHelper.FileHeader,
                            JsonConvert.SerializeObject(header));

                        MemoryStream ms = new MemoryStream(bytes);
                        return new FileStreamResult(ms, header.ContentType);
                    }
                    else
                        return NotFound();
                }
                else
                    await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(DownloadFile), LogEvent.EventsTypes.Permits, "Not Permits");

                return NotFound();
            }
            catch(Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsDashboardController), nameof(DownloadFile), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        [HttpGet("getreport/{id}")]
        public async Task<string?> GetReport(int id)
        {
            try
            {
                if (await _permitsService.CanDownloadInterventionReport(id))
                {
                    var ticketIntervention = await _context.TicketsInterventions.FindAsync(id);
                    if (ticketIntervention != null && ticketIntervention.HasAttachments)
                    {
                        byte[] bytes = _archiveService.GetAttachment(id, "pdf");

                        var s = "data:application/pdf;base64," + Convert.ToBase64String(bytes);

                       

                        return s;
                    }
                    else
                        return null;
                }
                else
                    await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(GetReport), LogEvent.EventsTypes.Permits, "Not Permits");

                return null;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketsDashboardController), nameof(GetReport), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }


        [HttpPost("Email/{Id}")]
        public async Task<ActionResult<bool>> SendReportIntervention(int id, [FromBody] EmailViewModel email)
        {
            var intervention = await _context.TicketsInterventions.FindAsync(id);

            
            if (intervention == null)
                return NotFound();

            if (!await _permitsService.CanGetTicket(intervention.IdTicket))
                return Problem("Not Permits");

            string path = _archiveService.GetPath(id, "pdf");

            List<string> listAttachment = new List<string>() { { path } };

            var state = await _emailSender.SendEmailAsync(email.To.ToList(";"), EmailsTypes.InvioDocumento, new List<string>() { { path } }, email.Subject, email.Message, null, email.CC);

            if (state)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(SendReportIntervention), LogEvent.EventsTypes.Info, "Email inviata correttamente");
            }
            else
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(SendReportIntervention), LogEvent.EventsTypes.Error, "Errore durante l'invio dell'email");

            return state;
        }

        private async Task InterventionType(int idTicketIntervention, List<int> interventionTypesId)
        {
            var intervention = await _context.TicketsInterventions.Include(x => x.TicketInterventionsTypes).Where(x => x.Id == idTicketIntervention).FirstOrDefaultAsync();

            if (intervention != null)
            {
                _context.Entry(intervention).State = EntityState.Modified;

                interventionTypesId = interventionTypesId ?? new List<int>();

                var list = intervention.TicketInterventionsTypes.Where(x => !interventionTypesId.Contains(x.Id)).ToList();
                foreach (var item in list)
                {
                    intervention.TicketInterventionsTypes.Remove(item);
                }

                foreach (var id in interventionTypesId)
                {
                    if (!intervention.TicketInterventionsTypes.Where(x => x.Id == id).Any())
                    {
                        var interventionType = await _context.InterventionTypes.FindAsync(id);
                        intervention.TicketInterventionsTypes.Add(interventionType);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }
       

        private async Task<string> CreatePdf(int id)
        {
            try
            {

                TicketIntervention intervention = await _context.TicketsInterventions.Include(x => x.User).Where(x => x.Id == id).FirstOrDefaultAsync();

                if (intervention == null)
                    return null;

                HtmlToPdf converter = new HtmlToPdf();
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 30;
                converter.Options.MarginRight = 30;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;
                
                converter.Options.DisplayFooter = true;
                converter.Footer.DisplayOnEvenPages = true;
                converter.Footer.DisplayOnOddPages = true;  
                converter.Footer.DisplayOnFirstPage = true;

                converter.Options.DisplayHeader = true;
                converter.Header.DisplayOnFirstPage = true;
                converter.Header.DisplayOnEvenPages = true;
                converter.Header.DisplayOnOddPages = true;
                
                converter.Header.Height = 45;
               

                converter.Options.RenderingEngine = RenderingEngine.Blink;

                string headerUrl = HttpContext.AbsoluteUrl("/Reports/Header",
                     new { id = intervention.User.IdCompany });

                PdfHtmlSection headerHtml = new PdfHtmlSection(headerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;
            
               converter.Header.Add(headerHtml);

                string footerUrl = HttpContext.AbsoluteUrl("/Reports/Footer");

                PdfHtmlSection footerHtml = new PdfHtmlSection(footerUrl);
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
                headerHtml.MinPageLoadTime = 1;

                PdfTextSection text = new PdfTextSection(0, 10,
                        "Page: {page_number} of {total_pages}  ",
                       new System.Drawing.Font("Arial", 8));

                text.HorizontalAlign = PdfTextHorizontalAlign.Right;

                converter.Footer.Add(footerHtml);
                converter.Footer.Add(text);
                converter.Footer.FirstPageNumber = 1;

                var absUrl = HttpContext.AbsoluteUrl("/Reports/InterventionModule", new { id = id, idLanguage = 1 });

                converter.Options.MinPageLoadTime = 1;
                converter.Options.MaxPageLoadTime = 300;
                PdfDocument doc = converter.ConvertUrl(absUrl);

                string path = _archiveService.GetPath(id, "pdf");
                // save pdf document 
                doc.Save(path);

                // close pdf document 
                doc.Close();

              
                return path;
            }

            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketInterventionsController), nameof(CreatePdf), LogEvent.EventsTypes.Error, ex.Message);
                return null;
            }
        }

        private async Task UpdateArticles(TicketIntervention ticketIntervention)
        {
            var articles =  await _context.TicketInterventionArticles.Where(x=>x.IdTicketIntervention == ticketIntervention.Id).ToListAsync();

            _context.TicketInterventionArticles.RemoveRange(articles);

            await _context.SaveChangesAsync();

            foreach (var item in ticketIntervention.InterventionArticles)
            {
                _context.TicketInterventionArticles.Add(new TicketInterventionArticle() { IdArticle = item.IdArticle, IdProduct = item.IdProduct, SerialNumber = item.SerialNumber, Description = item.Description, 
                    IdTicketIntervention = ticketIntervention.Id});
            }
            await _context.SaveChangesAsync();

        }
        private bool TicketInterventionExists(int id)
        {
            return _context.TicketsInterventions.Any(e => e.Id == id);
        }

        private bool ExistReport(int id)
        {
            string path = PathReportIntervention(id);

            return System.IO.File.Exists(path);
        }

        private string PathReportIntervention(int id)
        {
            return  Path.Combine(_archiveService.GetPath(), $"{id}.pdf");
        }

        private async Task<int?> TicketGetIdCompany(int idTicket)
        {
            var ticket = await _context.Tickets.FindAsync(idTicket);
            if (ticket != null)
            {
                return ticket.IdCompany;
            }
            else
                return null;
        }

        private string? ProductName(int? IdProduct)
        {
            var product = _context.Products.Find(IdProduct);

            return product?.Name;
        }

        public string? ArticleSN(int? IdArticle)
        {
            var article = _context.Articles.Find(IdArticle);
            return article?.SerialNumber;
        }

        //Gets the path of the PDF document
        private string GetDocumentPath(string value)
        {
            if (int.TryParse(value, out int id))
            {
                string path = _archiveService.GetPath(id, "pdf");

                return path;

            }
            else
                return string.Empty;

        }
    }
}
