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

        public static string Extract(byte[] bytes, string fileName)
        {
            var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => ExtractPdf(bytes),
                ".docx" => ExtractDocx(bytes),
                ".txt" or ".md" => Encoding.UTF8.GetString(bytes),
                _ => throw new NotSupportedException($"Formato non supportato: {ext}")
            };
        }

        private static string ExtractPdf(byte[] bytes)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(bytes);
            foreach (var page in document.GetPages())
            {
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
