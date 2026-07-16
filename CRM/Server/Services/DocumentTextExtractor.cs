using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;

namespace CRM.Server.Services
{
    /// <summary>
    /// Estrae il testo da documenti caricati (PDF, Word .docx, testo .txt/.md) per l'import
    /// nella base di conoscenza. I PDF scansionati (solo immagini) non producono testo.
    /// </summary>
    public static class DocumentTextExtractor
    {
        public static bool IsSupported(string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext is ".pdf" or ".docx" or ".txt" or ".md";
        }

        /// <param name="pageFrom">Prima pagina da estrarre (1-based, inclusa). Solo PDF; ignorato per gli altri formati. Null = dall'inizio.</param>
        /// <param name="pageTo">Ultima pagina da estrarre (1-based, inclusa). Solo PDF; ignorato per gli altri formati. Null = fino alla fine.</param>
        public static string Extract(byte[] bytes, string fileName, int? pageFrom = null, int? pageTo = null)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => ExtractPdf(bytes, pageFrom, pageTo),
                ".docx" => ExtractDocx(bytes),
                ".txt" or ".md" => Encoding.UTF8.GetString(bytes),
                _ => throw new NotSupportedException($"Formato non supportato: {ext}")
            };
        }

        private static string ExtractPdf(byte[] bytes, int? pageFrom = null, int? pageTo = null)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(bytes);

            var total = document.NumberOfPages;
            // Intervallo 1-based inclusivo, con clamp ai limiti reali del documento.
            // Campi vuoti (null) => intero documento.
            var start = pageFrom.HasValue ? Math.Max(1, pageFrom.Value) : 1;
            var end = pageTo.HasValue ? Math.Min(total, pageTo.Value) : total;
            if (pageFrom.HasValue && pageTo.HasValue && start > end)
                (start, end) = (end, start); // range invertito: normalizza

            for (var n = start; n <= end; n++)
            {
                var page = document.GetPage(n);
                // GetWords() ricostruisce le parole con la spaziatura corretta; fallback su Text
                var words = page.GetWords()?.Select(w => w.Text);
                var pageText = words != null ? string.Join(" ", words) : page.Text;
                sb.AppendLine(pageText);
            }
            return sb.ToString();
        }

        private static string ExtractDocx(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
                return string.Empty;

            var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
            return string.Join("\n", paragraphs);
        }
    }
}
