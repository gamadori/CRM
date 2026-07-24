using CRM.Shared;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    public interface ITicketPdfGenerator
    {
        byte[] GenerateTicketPdf(Ticket ticket);
    }

    public class TicketPdfGenerator : ITicketPdfGenerator
    {
        public byte[] GenerateTicketPdf(Ticket ticket)
        {
            // Configura la licenza QuestPDF (Community License per uso commerciale gratuito)
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header()
                        .Height(100)
                        .Background(Colors.Blue.Lighten4)
                        .Padding(20)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text("TICKET DETTAGLI")
                                    .FontSize(20)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken2);

                                column.Item().Text($"Ticket #{ticket.Id}")
                                    .FontSize(14)
                                    .FontColor(Colors.Grey.Darken1);
                            });

                            row.ConstantItem(150).Column(column =>
                            {
                                column.Item().AlignRight().Text($"Data Apertura:")
                                    .FontSize(10);
                                column.Item().AlignRight().Text(ticket.DateOpened.ToString("dd/MM/yyyy HH:mm"))
                                    .FontSize(10)
                                    .Bold();
                            });
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(15);

                            // Sezione Cliente
                            column.Item().Element(container => RenderSection(container, "Cliente", () =>
                            {
                                if (ticket.Company != null)
                                {
                                    return new[]
                                    {
                                        ("Ragione Sociale", ticket.Company.RagioneSociale ?? "N/A"),
                                        ("Partita IVA", ticket.Company.PIva ?? "N/A"),
                                        ("Contatto", ticket.Contact?.NameComplete ?? "N/A")
                                    };
                                }
                                return new[] { ("Cliente", "Non specificato") };
                            }));

                            // Sezione Ticket
                            column.Item().Element(container => RenderSection(container, "Informazioni Ticket", () =>
                            {
                                var priority = ticket.Priority switch
                                {
                                    0 => "Bassa",
                                    1 => "Media",
                                    2 => "Alta",
                                    _ => "N/A"
                                };

                                var items = new List<(string, string)>
                                {
                                    ("Tipo", ticket.TicketType?.ToString() ?? "N/A"),
                                    ("Priorit�", priority),
                                    ("Stato", ticket.State?.Description ?? (ticket.Closed ? "Chiuso" : "Aperto"))
                                };

                                if (ticket.Date.HasValue)
                                {
                                    var dateStr = ticket.Date.Value.ToString("dd/MM/yyyy");
                                    if (ticket.Time.HasValue)
                                        dateStr += $" {ticket.Time.Value:HH:mm}";
                                    items.Add(("Data Intervento", dateStr));
                                }

                                if (ticket.DateEnd.HasValue)
                                    items.Add(("Data Fine", ticket.DateEnd.Value.ToString("dd/MM/yyyy")));

                                if (!string.IsNullOrEmpty(ticket.IdUserAssigned))
                                    items.Add(("Assegnato a", ticket.UserAssigned?.NameComplete ?? ticket.IdUserAssigned));

                                return items.ToArray();
                            }));

                            // Sezione Prodotto/Articolo
                            if (ticket.Product != null || ticket.Article != null)
                            {
                                column.Item().Element(container => RenderSection(container, "Prodotto/Articolo", () =>
                                {
                                    var items = new List<(string, string)>();

                                    if (ticket.Product != null)
                                        items.Add(("Prodotto", ticket.Product.Name ?? "N/A"));

                                    if (ticket.Article != null)
                                    {
                                        items.Add(("Numero Seriale", ticket.Article.SerialNumber ?? "N/A"));
                                        if (ticket.Article.SaleDate.HasValue)
                                            items.Add(("Data Vendita", ticket.Article.SaleDate.Value.ToString("dd/MM/yyyy")));
                                    }

                                    return items.ToArray();
                                }));
                            }

                            // Sezione Descrizione
                            if (!string.IsNullOrWhiteSpace(ticket.Description))
                            {
                                column.Item().Element(container => RenderDescriptionSection(container, "Descrizione", ticket.Description));
                            }

                            // Sezione Chiusura (se chiuso)
                            if (ticket.Closed)
                            {
                                column.Item().Element(container => RenderSection(container, "Informazioni Chiusura", () =>
                                {
                                    var items = new List<(string, string)>();

                                    if (ticket.DateClosed.HasValue)
                                        items.Add(("Data Chiusura", ticket.DateClosed.Value.ToString("dd/MM/yyyy HH:mm")));

                                    if (!string.IsNullOrEmpty(ticket.IdUserClosed))
                                        items.Add(("Chiuso da", ticket.UserClosed?.NameComplete ?? ticket.IdUserClosed));

                                    if (!string.IsNullOrWhiteSpace(ticket.CloseDescription))
                                        items.Add(("Descrizione Chiusura", ticket.CloseDescription));

                                    return items.ToArray();
                                }));
                            }

                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Pagina ");
                            x.CurrentPageNumber();
                            x.Span(" di ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private static void RenderSection(IContainer container, string title, Func<(string Label, string Value)[]> getFields)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(15).Column(column =>
            {
                column.Item().PaddingBottom(10).Text(title)
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                var fields = getFields();
                foreach (var (label, value) in fields)
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(150).Text($"{label}:")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);

                        row.RelativeItem().Text(value ?? "N/A")
                            .FontSize(10);
                    });
                }
            });
        }

        private static void RenderDescriptionSection(IContainer container, string title, string description)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(15).Column(column =>
            {
                column.Item().PaddingBottom(10).Text(title)
                    .FontSize(14)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                column.Item().Text(description ?? "N/A")
                    .FontSize(10)
                    .LineHeight(1.5f);
            });
        }
    }
}
