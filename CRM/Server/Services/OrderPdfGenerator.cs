using System.Globalization;
using CRM.Shared;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    public interface IOrderPdfGenerator
    {
        byte[] Generate(Order order, Company? provider, byte[]? logoBytes);
    }

    /// <summary>
    /// Genera la "fattura di cortesia" (proforma) dell'ordine: documento PDF privo di valore fiscale.
    /// La fattura fiscale va emessa separatamente via SdI (fuori da questo modulo).
    /// </summary>
    public class OrderPdfGenerator : IOrderPdfGenerator
    {
        private static readonly CultureInfo Ci = new("it-IT");

        public byte[] Generate(Order order, Company? provider, byte[]? logoBytes)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, order, provider, logoBytes));
                    page.Content().PaddingVertical(15).Element(c => ComposeContent(c, order));
                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingBottom(3).Text("Documento di cortesia privo di valore fiscale. La fattura fiscale sara' emessa tramite Sistema di Interscambio (SdI).")
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        col.Item().AlignCenter().Text(x =>
                        {
                            x.Span("Pagina ");
                            x.CurrentPageNumber();
                            x.Span(" di ");
                            x.TotalPages();
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, Order order, Company? provider, byte[]? logoBytes)
        {
            container.Column(outer =>
            {
                outer.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                            col.Item().PaddingBottom(6).Width(140).Height(55).Image(logoBytes, ImageScaling.FitArea);

                        if (provider != null)
                        {
                            col.Item().Text(provider.RagioneSociale ?? "").FontSize(12).Bold();
                            var addr = provider.FormatIndirizzo;
                            if (!string.IsNullOrWhiteSpace(addr))
                                col.Item().Text(addr).FontSize(9).FontColor(Colors.Grey.Darken1);
                            var contacts = string.Join("  |  ", new[]
                            {
                                string.IsNullOrWhiteSpace(provider.Telefono) ? null : $"Tel. {provider.Telefono}",
                                string.IsNullOrWhiteSpace(provider.Email) ? null : provider.Email,
                                string.IsNullOrWhiteSpace(provider.Web) ? null : provider.Web
                            }.Where(s => s != null));
                            if (!string.IsNullOrWhiteSpace(contacts))
                                col.Item().Text(contacts).FontSize(9).FontColor(Colors.Grey.Darken1);
                            if (!string.IsNullOrWhiteSpace(provider.PIva))
                                col.Item().Text($"P.IVA {provider.PIva}").FontSize(9).FontColor(Colors.Grey.Darken1);
                        }
                    });

                    row.ConstantItem(200).Column(col =>
                    {
                        col.Item().AlignRight().Text("FATTURA DI CORTESIA").FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        col.Item().AlignRight().Text(order.Number ?? "").FontSize(13).FontColor(Colors.Grey.Darken1);
                        col.Item().AlignRight().Text($"Data: {order.Date:dd/MM/yyyy}").FontSize(10);
                        if (order.DeliveryDate.HasValue)
                            col.Item().AlignRight().Text($"Consegna: {order.DeliveryDate.Value:dd/MM/yyyy}").FontSize(10);
                        col.Item().AlignRight().Text($"Stato: {StateText(order.State)}").FontSize(10).Bold();
                    });
                });

                outer.Item().PaddingTop(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
            });
        }

        private static void ComposeContent(IContainer container, Order order)
        {
            container.Column(col =>
            {
                col.Spacing(15);

                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
                    PdfClientBlock.Compose(c, order.Company, order.Contact?.NameComplete));

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.6f);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header, "Descrizione", TextAlignment.Left);
                        HeaderCell(header, "Q.tà", TextAlignment.Right);
                        HeaderCell(header, "Prezzo", TextAlignment.Right);
                        HeaderCell(header, "Sc.%", TextAlignment.Right);
                        HeaderCell(header, "IVA%", TextAlignment.Right);
                        HeaderCell(header, "Totale", TextAlignment.Right);
                    });

                    foreach (var r in order.Rows.OrderBy(r => r.SortOrder))
                    {
                        BodyCell(table, r.Description, TextAlignment.Left);
                        BodyCell(table, r.Quantity.ToString("0.##", Ci), TextAlignment.Right);
                        BodyCell(table, r.UnitPrice.ToString("C", Ci), TextAlignment.Right);
                        BodyCell(table, r.DiscountPct.ToString("0.##", Ci), TextAlignment.Right);
                        BodyCell(table, r.VatRate.ToString("0.##", Ci), TextAlignment.Right);
                        BodyCell(table, r.LineTotal.ToString("C", Ci), TextAlignment.Right);
                    }
                });

                col.Item().AlignRight().Width(240).Column(c =>
                {
                    TotalRow(c, "Imponibile", order.Subtotal, false);
                    TotalRow(c, "Sconto", order.TotalDiscount, false);
                    TotalRow(c, "IVA", order.TotalVat, false);
                    c.Item().PaddingTop(4).BorderTop(1).BorderColor(Colors.Grey.Medium);
                    TotalRow(c, "Totale", order.Total, true);
                });

                if (!string.IsNullOrWhiteSpace(order.Note))
                {
                    col.Item().Column(c =>
                    {
                        c.Item().Text("Note").FontSize(11).Bold().FontColor(Colors.Blue.Darken1);
                        c.Item().Text(order.Note).FontSize(9).LineHeight(1.4f);
                    });
                }
            });
        }

        private static void HeaderCell(TableCellDescriptor header, string text, TextAlignment align)
        {
            IContainer cell = header.Cell().Background(Colors.Blue.Lighten4).Padding(5);
            if (align == TextAlignment.Right) cell = cell.AlignRight();
            cell.Text(text).FontSize(9).Bold();
        }

        private static void BodyCell(TableDescriptor table, string text, TextAlignment align)
        {
            IContainer cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
            if (align == TextAlignment.Right) cell = cell.AlignRight();
            cell.Text(text ?? "").FontSize(9);
        }

        private static void TotalRow(ColumnDescriptor col, string label, decimal value, bool strong)
        {
            col.Item().Row(row =>
            {
                var l = row.RelativeItem().Text(label).FontSize(strong ? 12 : 10);
                var v = row.ConstantItem(110).AlignRight().Text(value.ToString("C", Ci)).FontSize(strong ? 12 : 10);
                if (strong)
                {
                    l.Bold();
                    v.Bold();
                }
            });
        }

        private enum TextAlignment { Left, Right }

        private static string StateText(OrderStates s) => s switch
        {
            OrderStates.Confirmed => "Confermato",
            OrderStates.InProduction => "In produzione",
            OrderStates.Delivered => "Consegnato",
            OrderStates.Invoiced => "Fatturato",
            OrderStates.Cancelled => "Annullato",
            _ => s.ToString()
        };
    }
}
