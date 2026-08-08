using System.Globalization;
using CRM.Shared;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    /// <summary>
    /// Una riga del prospetto: la spesa come va letta su carta, non come sta a database.
    /// </summary>
    public class ExpenseReportRow
    {
        public DateTime Date { get; set; }

        public string SpenderName { get; set; } = string.Empty;

        public string MerchantName { get; set; } = string.Empty;

        public string Context { get; set; } = string.Empty;

        public decimal? Amount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public decimal? AmountBase { get; set; }

        /// <summary>Importo presente ma senza conversione: non entra in nessun totale.</summary>
        public bool NeedsConversion => Amount.HasValue && !AmountBase.HasValue;

        public bool IsConfirmed { get; set; }
    }

    /// <summary>
    /// Le spese di una tipologia, con il loro subtotale. E' il raggruppamento per cui il prospetto
    /// esiste: vitto, alloggio e trasporti hanno trattamenti fiscali diversi, quindi un elenco che
    /// non li separa non serve a chi deve dedurli.
    /// </summary>
    public class ExpenseReportGroup
    {
        /// <summary>Null e' un gruppo legittimo: le spese ancora da classificare.</summary>
        public ExpenseCategory? Category { get; set; }

        public string Label { get; set; } = string.Empty;

        public List<ExpenseReportRow> Rows { get; set; } = new();

        /// <summary>Somma dei soli importi convertiti: gli altri sono contati a parte.</summary>
        public decimal TotalBase { get; set; }

        public int NeedsConversionCount { get; set; }
    }

    /// <summary>
    /// Tutto quello che serve a stampare il prospetto, gia' calcolato. Il generatore non fa conti:
    /// se li facesse, i totali sulla carta e quelli a schermo potrebbero divergere senza che
    /// nessuno se ne accorga.
    /// </summary>
    public class ExpenseReportData
    {
        /// <summary>Di che cosa e' il prospetto: la fiera, l'intervento, la persona, il periodo.</summary>
        public string Title { get; set; } = "Nota spese";

        /// <summary>Righe di contesto sotto il titolo: periodo, persona, filtri applicati.</summary>
        public List<string> Context { get; set; } = new();

        public string BaseCurrency { get; set; } = "EUR";

        public List<ExpenseReportGroup> Groups { get; set; } = new();

        public int RowCount { get; set; }

        public decimal TotalBase { get; set; }

        public decimal TotalTaxBase { get; set; }

        public int NeedsConversionCount { get; set; }

        /// <summary>Vero se chi ha chiesto il prospetto vede solo le proprie spese: va scritto.</summary>
        public bool PartialView { get; set; }

        /// <summary>Mostrare la colonna "Persona" ha senso solo se ce n'e' piu' d'una.</summary>
        public bool ShowSpender { get; set; }

        public Company? Provider { get; set; }

        public byte[]? LogoBytes { get; set; }

        public string FileName { get; set; } = "note-spese.pdf";
    }

    public interface IExpenseReportPdfGenerator
    {
        byte[] Generate(ExpenseReportData data);
    }

    /// <summary>
    /// Prospetto delle note spese raggruppate per tipologia.
    /// <para>
    /// Non e' un documento fiscale e lo dichiara in fondo: manca la conservazione a norma dei
    /// giustificativi, che senza firma e marca temporale la foto dello scontrino non sostituisce.
    /// E' il riepilogo che si porta al commercialista insieme ai cartacei.
    /// </para>
    /// </summary>
    public class ExpenseReportPdfGenerator : IExpenseReportPdfGenerator
    {
        private static readonly CultureInfo Ci = new("it-IT");

        public byte[] Generate(ExpenseReportData data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, data));
                    page.Content().PaddingVertical(10).Element(c => ComposeContent(c, data));
                    page.Footer().Element(c => ComposeFooter(c, data));
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, ExpenseReportData data)
        {
            container.Column(outer =>
            {
                outer.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        if (data.LogoBytes is { Length: > 0 })
                            col.Item().PaddingBottom(6).Width(130).Height(45).Image(data.LogoBytes, ImageScaling.FitArea);

                        if (data.Provider != null)
                        {
                            col.Item().Text(data.Provider.RagioneSociale ?? string.Empty).FontSize(11).Bold();
                            if (!string.IsNullOrWhiteSpace(data.Provider.PIva))
                                col.Item().Text($"P.IVA {data.Provider.PIva}").FontSize(8).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.ConstantItem(240).Column(col =>
                    {
                        col.Item().AlignRight().Text("NOTE SPESE").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().AlignRight().Text(data.Title).FontSize(11);

                        foreach (var line in data.Context)
                            col.Item().AlignRight().Text(line).FontSize(8).FontColor(Colors.Grey.Darken1);

                        col.Item().AlignRight().Text($"Stampato il {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                outer.Item().PaddingTop(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
            });
        }

        private static void ComposeContent(IContainer container, ExpenseReportData data)
        {
            container.Column(col =>
            {
                col.Spacing(12);

                col.Item().Element(c => ComposeSummary(c, data));

                foreach (var group in data.Groups)
                    col.Item().Element(c => ComposeGroup(c, group, data));

                col.Item().Element(c => ComposeTotal(c, data));

                if (data.NeedsConversionCount > 0)
                {
                    // Un totale che tace le spese non convertite vale meno di un totale mancante:
                    // si dice quante sono, non si nascondono.
                    col.Item().Text(
                        $"{data.NeedsConversionCount} " +
                        (data.NeedsConversionCount == 1 ? "spesa e' priva" : "spese sono prive") +
                        $" di valuta o cambio: sono elencate ma NON entrano nei totali.")
                        .FontSize(8).Italic().FontColor(Colors.Orange.Darken2);
                }

                if (data.PartialView)
                {
                    col.Item().Text("Prospetto limitato alle spese dell'utente che l'ha richiesto.")
                        .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                }
            });
        }

        private static void ComposeSummary(IContainer container, ExpenseReportData data)
        {
            container.Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Totale").FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(Money(data.TotalBase, data.BaseCurrency)).FontSize(14).Bold();
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Note spese").FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(data.RowCount.ToString(Ci)).FontSize(14).Bold();
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Tipologie").FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(data.Groups.Count.ToString(Ci)).FontSize(14).Bold();
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"IVA (in {data.BaseCurrency})").FontSize(8).FontColor(Colors.Grey.Darken2);
                    col.Item().Text(Money(data.TotalTaxBase, data.BaseCurrency)).FontSize(14).Bold();
                });
            });
        }

        private static void ComposeGroup(IContainer container, ExpenseReportGroup group, ExpenseReportData data)
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(3).Row(row =>
                {
                    row.RelativeItem().Text(group.Label).FontSize(11).Bold();
                    row.ConstantItem(140).AlignRight().Text(Money(group.TotalBase, data.BaseCurrency)).FontSize(11).Bold();
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(55);                       // data
                        if (data.ShowSpender) c.RelativeColumn(2);  // persona
                        c.RelativeColumn(3);                        // esercente
                        c.RelativeColumn(2.5f);                     // contesto
                        c.RelativeColumn(1.6f);                     // importo originale
                        c.RelativeColumn(1.6f);                     // in valuta base
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Data");
                        if (data.ShowSpender) header.Cell().Element(HeaderCell).Text("Persona");
                        header.Cell().Element(HeaderCell).Text("Esercente");
                        header.Cell().Element(HeaderCell).Text("Contesto");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Importo");
                        header.Cell().Element(HeaderCell).AlignRight().Text($"In {data.BaseCurrency}");
                    });

                    foreach (var row in group.Rows)
                    {
                        table.Cell().Element(BodyCell).Text(row.Date.ToString("dd/MM/yyyy"));
                        if (data.ShowSpender) table.Cell().Element(BodyCell).Text(row.SpenderName);
                        table.Cell().Element(BodyCell).Text(row.MerchantName);
                        table.Cell().Element(BodyCell).Text(row.Context).FontColor(Colors.Grey.Darken1);
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(row.Amount, row.Currency));

                        var converted = table.Cell().Element(BodyCell).AlignRight();
                        if (row.NeedsConversion)
                            converted.Text("da convertire").FontColor(Colors.Orange.Darken2);
                        else
                            converted.Text(Money(row.AmountBase, data.BaseCurrency));
                    }
                });

                if (group.NeedsConversionCount > 0)
                {
                    col.Item().PaddingTop(2).Text(
                        $"({group.NeedsConversionCount} non " +
                        (group.NeedsConversionCount == 1 ? "convertita" : "convertite") +
                        ", fuori dal subtotale)")
                        .FontSize(7).Italic().FontColor(Colors.Orange.Darken2);
                }
            });
        }

        private static void ComposeTotal(IContainer container, ExpenseReportData data)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("TOTALE COMPLESSIVO").FontSize(12).Bold();
                row.ConstantItem(160).AlignRight().Text(Money(data.TotalBase, data.BaseCurrency)).FontSize(12).Bold();
            });
        }

        private static void ComposeFooter(IContainer container, ExpenseReportData data)
        {
            container.Column(col =>
            {
                col.Item().PaddingBottom(3).Text(
                    $"Importi convertiti in {data.BaseCurrency} al cambio congelato al momento della registrazione. " +
                    "Prospetto di riepilogo: non sostituisce i giustificativi originali.")
                    .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);

                col.Item().AlignCenter().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontSize(8));
                    x.Span("Pagina ");
                    x.CurrentPageNumber();
                    x.Span(" di ");
                    x.TotalPages();
                });
            });
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(3).PaddingHorizontal(2)
                .DefaultTextStyle(x => x.FontSize(8).SemiBold().FontColor(Colors.Grey.Darken2));

        private static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(2)
                .DefaultTextStyle(x => x.FontSize(8));

        /// <summary>
        /// Importo con la sua valuta. Mai un simbolo inventato: se la valuta non c'e' si stampa il
        /// numero e basta, come fa il resto del modulo.
        /// </summary>
        private static string Money(decimal? amount, string? currency)
        {
            if (!amount.HasValue)
                return "-";

            var value = amount.Value.ToString("N2", Ci);
            return string.IsNullOrWhiteSpace(currency) ? value : $"{value} {currency}";
        }
    }
}
