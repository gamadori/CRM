using CRM.Shared;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    /// <summary>
    /// Blocco "Cliente" condiviso tra i PDF (preventivo, fattura di cortesia).
    /// Mostra ragione sociale e i dati anagrafici/fiscali del cliente, inserendo
    /// ogni campo solo se valorizzato, così il cliente può verificare i dati a db.
    /// </summary>
    internal static class PdfClientBlock
    {
        public static void Compose(ColumnDescriptor c, Company? company, string? contactName)
        {
            c.Item().PaddingBottom(6).Text("Cliente").FontSize(12).Bold().FontColor(Colors.Blue.Darken1);
            c.Item().Text(company?.RagioneSociale ?? "N/D").FontSize(11).Bold();

            if (company != null)
            {
                if (!string.IsNullOrWhiteSpace(company.Indirizzo))
                    Line(c, company.Indirizzo!);

                var location = string.Join(" ", new[]
                {
                    string.IsNullOrWhiteSpace(company.Cap) ? null : company.Cap!.Trim(),
                    string.IsNullOrWhiteSpace(company.Citta) ? null : company.Citta!.Trim()
                }.Where(s => s != null));

                if (!string.IsNullOrWhiteSpace(company.Stato))
                    location = string.IsNullOrWhiteSpace(location)
                        ? company.Stato!.Trim()
                        : $"{location} - {company.Stato!.Trim()}";

                if (!string.IsNullOrWhiteSpace(location))
                    Line(c, location);

                if (!string.IsNullOrWhiteSpace(company.PIva))
                    Line(c, $"P.IVA {company.PIva}");

                if (!string.IsNullOrWhiteSpace(company.CodiceFiscale))
                    Line(c, $"C.F. {company.CodiceFiscale}");

                if (!string.IsNullOrWhiteSpace(company.CodiceSDI))
                    Line(c, $"Cod. SDI {company.CodiceSDI}");
            }

            if (!string.IsNullOrWhiteSpace(contactName))
                Line(c, $"Rif. {contactName}");
        }

        private static void Line(ColumnDescriptor c, string text) =>
            c.Item().Text(text).FontSize(9).FontColor(Colors.Grey.Darken1);
    }
}
