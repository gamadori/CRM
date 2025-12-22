using CRM.Server.Data;
using CRM.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Syncfusion.EJ2.PdfViewer;
using System;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PDFInterventionsController : ControllerBase
    {
        private IWebHostEnvironment _hostingEnvironment;
        //Initialize the memory cache object   
        public readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;
        private readonly IArchiveService _archiveService;
         
        public PDFInterventionsController(ApplicationDbContext context, IWebHostEnvironment hostingEnvironment, IMemoryCache cache, 
            IArchiveService archiveService)
        {
            _hostingEnvironment = hostingEnvironment;
            _cache = cache;
            _context = context;
            _archiveService = archiveService;
            _archiveService.TypeArchive = ArchiveTypes.Interventions;
        }

        [HttpPost("Load")]
        
        [Route("[controller]/Load")]
        [AcceptVerbs("Post")]

        //Post action for Loading the PDF documents   
        public IActionResult Load([FromBody] Dictionary<string, object> jsonObject)
        {
            Console.WriteLine("Load called");
            //Initialize the PDF viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            MemoryStream stream = new MemoryStream();
            object jsonResult = new object();
            if (jsonObject != null && jsonObject.ContainsKey("document"))
            {
                if (bool.Parse(jsonObject["isFileName"].ToString()))
                {
                    string documentPath = GetDocumentPath(jsonObject["document"].ToString());
                    if (!string.IsNullOrEmpty(documentPath) && System.IO.File.Exists(documentPath))
                    {
                        byte[] bytes = System.IO.File.ReadAllBytes(documentPath);
                        stream = new MemoryStream(bytes);
                    }
                    else
                    {
                        return this.Content(jsonObject["document"] + " is not found");
                    }
                }
                else
                {
                    byte[] bytes = Convert.FromBase64String(jsonObject["document"].ToString());
                    stream = new MemoryStream(bytes);
                }
            }
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());
            jsonResult = pdfviewer.Load(stream, dString);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        

        [AcceptVerbs("Post")]
        [HttpPost("Bookmarks")]

        [Route("[controller]/Bookmarks")]
        //Post action for processing the bookmarks from the PDF documents
        public IActionResult Bookmarks([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            var jsonResult = pdfviewer.GetBookmarks(dString);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost("RenderPdfPages")]

        [Route("[controller]/RenderPdfPages")]
        //Post action for processing the PDF documents  
        public IActionResult RenderPdfPages([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.GetPage(dString);
            var result = JsonConvert.SerializeObject(jsonResult);

            //var bytes = Encoding.UTF8.GetBytes(jsonResult.ToString());
            //return File(jsonResult, "application/octet-stream");
            return Content(result);
        }

        [AcceptVerbs("Post")]
        [HttpPost("RenderThumbnailImages")]

        [Route("[controller]/RenderThumbnailImages")]
        //Post action for rendering the ThumbnailImages
        public IActionResult RenderThumbnailImages([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object result = pdfviewer.GetThumbnailImages(dString);
            return Content(JsonConvert.SerializeObject(result));
        }
        [AcceptVerbs("Post")]
        [HttpPost("RenderAnnotationComments")]

        [Route("[controller]/RenderAnnotationComments")]
        //Post action for rendering the annotations
        public IActionResult RenderAnnotationComments([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.GetAnnotationComments(dString);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }
        [AcceptVerbs("Post")]
        [HttpPost("ExportAnnotations")]

        [Route("[controller]/ExportAnnotations")]
        //Post action to export annotations
        public IActionResult ExportAnnotations([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);

            string jsonResult = pdfviewer.GetAnnotationComments(dString).ToString();
            return Content(jsonResult);
        }
        [AcceptVerbs("Post")]
        [HttpPost("ImportAnnotations")]

        [Route("[controller]/ImportAnnotations")]
        //Post action to import annotations
        public IActionResult ImportAnnotations([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            string jsonResult = string.Empty;
            if (dString != null && dString.ContainsKey("fileName"))
            {
                string documentPath = GetDocumentPath(dString["fileName"]);
                if (!string.IsNullOrEmpty(documentPath))
                {
                    jsonResult = System.IO.File.ReadAllText(documentPath);
                }
                else
                {
                    return this.Content(dString["document"] + " is not found");
                }
            }
            return Content(jsonResult);
        }

        [AcceptVerbs("Post")]
        [HttpPost("ExportFormFields")]

        [Route("[controller]/ExportFormFields")]
        public IActionResult ExportFormFields([FromBody] Dictionary<string, object> jsonObject)

        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            string jsonResult = pdfviewer.ExportFormFields(dString);
            return Content(jsonResult);
        }

        [AcceptVerbs("Post")]
        [HttpPost("ImportFormFields")]

        [Route("[controller]/ImportFormFields")]
        public IActionResult ImportFormFields([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object jsonResult = pdfviewer.ImportFormFields(dString);
            return Content(JsonConvert.SerializeObject(jsonResult));
        }

        [AcceptVerbs("Post")]
        [HttpPost("Unload")]

        [Route("[controller]/Unload")]
        //Post action for unloading and disposing the PDF document resources  
        public IActionResult Unload([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            pdfviewer.ClearCache(dString);
            return this.Content("Document cache is cleared");
        }


        [HttpPost("Download")]

        [Route("[controller]/Download")]
        //Post action for downloading the PDF documents
        public IActionResult Download([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            string documentBase = pdfviewer.GetDocumentAsBase64(dString);
            return Content(documentBase);
        }

        [HttpPost("PrintImages")]

        [Route("[controller]/PrintImages")]
        //Post action for printing the PDF documents
        public IActionResult PrintImages([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            //Initialize the PDF Viewer object with memory cache object
            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object pageImage = pdfviewer.GetPrintImage(dString);
            return Content(JsonConvert.SerializeObject(pageImage));
        }

        [HttpPost("RenderPdfTexts")]
        [Route("[controller]/RenderPdfTexts")]
        public IActionResult RenderPdfTexts([FromBody] Dictionary<string, object> jsonObject)
        {
            Dictionary<string, string> dString = jsonObject.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

            PdfRenderer pdfviewer = new PdfRenderer(_cache);
            object result = pdfviewer.GetDocumentText(dString);
            return Content(JsonConvert.SerializeObject(result));
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
