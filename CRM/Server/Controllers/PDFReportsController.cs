using CRM.Server.Data;
using CRM.Server.Helpers;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.EntityFrameworkCore;
using SelectPdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PDFReportsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
        public PDFReportsController(ApplicationDbContext context, IArchiveService archiveService)
        {
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Interventions;
        }






        // GET: api/Companies/5
        [HttpGet("{id}")]
        public async Task<ActionResult> GetReport(int id)
        {
            try
            {
                

                TicketIntervention intervention =  await _context.TicketsInterventions.Include(x=>x.User).Where(x=>x.Id == id).FirstOrDefaultAsync();

                if (intervention == null)
                    return NotFound();

                HtmlToPdf converter = new HtmlToPdf();
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginLeft = 80;
                converter.Options.MarginRight = 80;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;

                converter.Options.DisplayHeader = true;
               
                converter.Header.DisplayOnFirstPage = true;
                converter.Header.DisplayOnEvenPages = true;
                converter.Header.DisplayOnOddPages = true;
                converter.Header.Height = 30;
                converter.Options.MinPageLoadTime = 2;

                PdfHtmlSection headerHtml = new PdfHtmlSection(HttpContext.AbsoluteUrl("/Reports/Header",
                     new { id = intervention.User.IdCompany}));
                headerHtml.AutoFitHeight = HtmlToPdfPageFitMode.AutoFit;
           
                
                converter.Header.Add(headerHtml);
             
                
                PdfTextSection text = new PdfTextSection(0, 10,
                        "Page: {page_number} of {total_pages}  ",
                        new System.Drawing.Font("Arial", 8));
                text.HorizontalAlign = PdfTextHorizontalAlign.Right;
                converter.Footer.Add(text);

                var absUrl = HttpContext.AbsoluteUrl("/Reports/InterventionModule", new { id = id, idLanguage = 1 });
                converter.Options.MaxPageLoadTime = 300;
                PdfDocument doc = converter.ConvertUrl(absUrl);

                string path = _archiveService.GetPath(id, "pdf");
                // save pdf document 
                doc.Save(path);

                // close pdf document 
                doc.Close();

                //FileResult fileResult = new FileContentResult(pdf, "application/pdf");
                //fileResult.FileDownloadName = "Document.pdf";
                return NotFound();
            }

            catch (Exception ex)
            {
                return NotFound();
            }
        }
        

        


    }
}
