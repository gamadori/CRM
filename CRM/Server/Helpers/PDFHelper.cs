using CRM.Server.Extensions;
using SelectPdf;

namespace CRM.Server.Helpers
{
    public  static class PDFHelper
    {
        public static byte[] ConvertHtmlToPdf(string html)
        {

            var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
            var pdfBytes = htmlToPdf.GeneratePdf(html);

            return pdfBytes;
        }

        public static string ConvertUriToPdf(string uri)
        {
            var htmlToPdf = new NReco.PdfGenerator.HtmlToPdfConverter();
            Stream stream = new MemoryStream();

            htmlToPdf.GeneratePdfFromFile(uri, null, stream);

            return stream.ToBase64String();

        }
    }
}
