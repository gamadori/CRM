using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    public interface ITicketPdfGenerator
    {
        Task<byte[]> GenerateTicketPdfAsync(int ticketId);
    }

    /// <summary>
    /// Scheda ticket in PDF. E' un documento che finisce in mano al cliente: contiene solo dati
    /// che ha senso mostrargli (niente priorita', niente note interne, niente smistamento AI) e
    /// segue l'impaginazione del verbale di intervento — intestazione con azienda e logo, titolo
    /// su fascia grigia, riquadri bordati con tabelle etichetta/valore, piede con i recapiti.
    /// </summary>
    public class TicketPdfGenerator : ITicketPdfGenerator
    {
        private readonly ApplicationDbContext _context;

        private Company? _company;
        private int? _logoId;

        public TicketPdfGenerator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateTicketPdfAsync(int ticketId)
            => (await BuildDocumentAsync(ticketId)).GeneratePdf();

        /// <summary>Documento prima della resa in PDF: separato per poterlo rendere in immagine
        /// nei controlli di impaginazione, dove il PDF non e' ispezionabile.</summary>
        internal async Task<Document> BuildDocumentAsync(int ticketId)
        {
            var ticket = await _context.Tickets
                .Include(x => x.Company)
                .Include(x => x.Contact)
                .Include(x => x.TicketType)
                .Include(x => x.Product)
                .Include(x => x.Article).ThenInclude(a => a!.Product)
                .Include(x => x.GroupAssigned)
                .Include(x => x.State)
                .Include(x => x.UserOpened)
                .Include(x => x.UserAssigned)
                .Include(x => x.UserClosed)
                .Include(x => x.CommessaFase).ThenInclude(f => f!.Commessa)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId)
                ?? throw new InvalidOperationException($"Ticket #{ticketId} non trovato");

            var interventi = await _context.TicketsInterventions
                .Include(i => i.TicketInterventionTime)
                .Include(i => i.AssignedUsers).ThenInclude(u => u.User)
                .Where(i => i.IdTicket == ticketId)
                .OrderBy(i => i.StartDateTime)
                .AsNoTracking()
                .ToListAsync();

            _company = await _context.GetHeadCompanyAsync();
            _logoId = (await _context.GlobalSettings.AsNoTracking().FirstOrDefaultAsync())?.LogoReport;

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    // Nessuna FontFamily esplicita: si usa il font incorporato in QuestPDF, che ha
                    // tutte le lettere accentate. Con "Arial" bastava che il font non fosse
                    // installato sulla macchina perche' accenti e simboli uscissero come quadratini.
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(HeaderBlock);

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Element(c => TitleBlock(c, $"SCHEDA TICKET {Numero(ticket)}"));

                        col.Item().PaddingTop(8).Element(c => FieldsBlock(c, "Dati del ticket", TicketFields(ticket)));

                        if (ticket.Company != null)
                            col.Item().PaddingTop(10).Element(c => FieldsBlock(c, "Cliente", ClientFields(ticket)));

                        var prodotto = ProductFields(ticket);
                        if (prodotto.Count > 0)
                            col.Item().PaddingTop(10).Element(c => FieldsBlock(c, "Prodotto", prodotto));

                        col.Item().PaddingTop(10).Element(c => TextAreaBlock(c, "Descrizione", ticket.Description));

                        if (interventi.Count > 0)
                            col.Item().PaddingTop(10).Element(c => InterventionsBlock(c, interventi));

                        if (ticket.Closed)
                            col.Item().PaddingTop(10).Element(c => ClosureBlock(c, ticket));
                    });

                    page.Footer().Element(FooterBlock);
                });
            });

            return document;
        }

        // ─── Blocchi ─────────────────────────────────────────────────────────────

        private void HeaderBlock(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        if (_company != null)
                        {
                            col.Item().Text(_company.RagioneSociale).SemiBold().FontSize(12);
                            col.Item().Text($"{_company.Indirizzo} {_company.Cap} {_company.Citta} {_company.Stato}");
                            col.Item().Text($"{_company.Telefono} {_company.Fax}");
                            col.Item().Text($"{_company.Email} | {_company.Web}");

                            // "VAT" da solo, con la partita IVA non compilata, sembrava un errore di stampa.
                            if (!string.IsNullOrWhiteSpace(_company.PIva))
                                col.Item().Text($"P. IVA {_company.PIva}").FontColor(Colors.Grey.Darken1).FontSize(9);
                        }
                    });

                    if (_logoId.HasValue)
                        row.ConstantItem(120).AlignRight().Element(RenderLogo);
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
        }

        private void RenderLogo(IContainer container)
        {
            try
            {
                var logo = _context.Logos.AsNoTracking().FirstOrDefault(l => l.Id == _logoId!.Value);
                if (logo == null || string.IsNullOrWhiteSpace(logo.InputFile))
                    return;

                var base64 = logo.InputFile.Contains(',') ? logo.InputFile.Split(',')[1] : logo.InputFile;
                var bytes = Convert.FromBase64String(base64);
                if (bytes.Length <= 8)
                    return;

                bool isPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                bool isJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8;

                if (isPng || isJpeg)
                    container.Width(120).Height(60).Image(bytes, ImageScaling.FitArea);
            }
            catch (Exception ex)
            {
                // Un logo illeggibile non deve far fallire la stampa del ticket.
                Console.WriteLine($"Errore caricamento logo: {ex.Message}");
            }
        }

        private static void TitleBlock(IContainer container, string title)
            => container
                .PaddingVertical(6)
                .Background(Colors.Grey.Lighten4)
                .Border(1).BorderColor(Colors.Grey.Lighten2)
                .AlignCenter()
                .Text(title).SemiBold().FontSize(14);

        private static List<(string Label, string? Value)> TicketFields(Ticket ticket)
        {
            var appuntamento = ticket.Date == null
                ? null
                : ticket.Date.Value.ToString("dd/MM/yyyy") + (ticket.Time == null ? "" : $" {ticket.Time:HH\\:mm}");

            return new List<(string, string?)>
            {
                ("Numero", Numero(ticket)),
                ("Tipo", ticket.TicketType?.Desc),
                ("Stato", StatoTicket(ticket)),
                ("Apertura", ticket.DateOpened.ToString("dd/MM/yyyy HH:mm")),
                ("Aperto da", ticket.UserOpened?.NameComplete),
                ("Assegnato a", ticket.UserAssigned?.NameComplete),
                ("Gruppo", ticket.GroupAssigned?.Name),
                ("Appuntamento", appuntamento),
                ("Fine prevista", Data(ticket.DateEnd)),
                ("Scadenza", Data(ticket.DateExpired)),
                ("Commessa", ticket.CommessaFase?.Commessa?.Code),
                ("Fase", ticket.CommessaFase?.Name)
            };
        }

        private static List<(string Label, string? Value)> ClientFields(Ticket ticket)
        {
            var company = ticket.Company!;

            return new List<(string, string?)>
            {
                ("Ragione sociale", company.RagioneSociale),
                ("P. IVA", company.PIva),
                ("Indirizzo", company.Indirizzo),
                ("CAP", company.Cap),
                ("Città", company.Citta),
                ("Provincia", company.Provincia),
                ("Referente", ticket.Contact?.NameComplete),
                ("Telefono", company.Telefono)
            };
        }

        private static List<(string Label, string? Value)> ProductFields(Ticket ticket)
            => new List<(string, string?)>
            {
                ("Prodotto", ticket.Product?.Name ?? ticket.Article?.Product?.Name),
                ("Matricola", ticket.Article?.SerialNumber),
                ("Data vendita", ticket.Article?.SaleDate?.ToString("dd/MM/yyyy"))
            }
            .Where(f => !string.IsNullOrWhiteSpace(f.Item2))
            .ToList();

        /// <summary>
        /// Riquadro di coppie etichetta/valore, due per riga. I campi vuoti si saltano: su un
        /// documento che va al cliente una colonna di etichette senza valore sembra un errore.
        /// </summary>
        private static void FieldsBlock(IContainer container, string title, List<(string Label, string? Value)> fields)
        {
            var visibili = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();

            container.ShowEntire().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(title).SemiBold();

                col.Item().PaddingTop(6).Table(t =>
                {
                    FourColumns(t);

                    for (int i = 0; i < visibili.Count; i += 2)
                    {
                        var sinistra = visibili[i];
                        var destra = i + 1 < visibili.Count ? visibili[i + 1] : (Label: "", Value: (string?)"");

                        Row(t, sinistra.Label, sinistra.Value!, destra.Label, destra.Value ?? "");
                    }
                });
            });
        }

        /// <summary>
        /// Interventi con le ore fatturabili, la stessa ripartizione della scheda ticket a video:
        /// lavoro, viaggio ed eventuali pause marcate fatturabili, piu' il totale.
        /// </summary>
        private static void InterventionsBlock(IContainer container, List<TicketIntervention> interventi)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text("Interventi").SemiBold();

                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(70);   // data
                        cols.RelativeColumn();     // tecnici
                        cols.ConstantColumn(60);   // lavoro
                        cols.ConstantColumn(60);   // viaggio
                        cols.ConstantColumn(70);   // fatturabile
                    });

                    t.Cell().Element(HeaderCell).Text("Data");
                    t.Cell().Element(HeaderCell).Text("Tecnico");
                    t.Cell().Element(HeaderCell).AlignRight().Text("Lavoro");
                    t.Cell().Element(HeaderCell).AlignRight().Text("Viaggio");
                    t.Cell().Element(HeaderCell).AlignRight().Text("Fatturabili");

                    int totLavoro = 0, totViaggio = 0, totPausa = 0, totFatturabile = 0;

                    foreach (var intervento in interventi)
                    {
                        var tempi = intervento.TicketInterventionTime ?? new List<TicketInterventionTime>();
                        var fatturabili = tempi.Where(x => x.IsBillable).ToList();

                        int lavoro = fatturabili.Where(x => x.TimeType == InterventionTimeType.Work).Sum(x => x.DurationMinutes);
                        int viaggio = fatturabili.Where(x => x.TimeType == InterventionTimeType.Travel).Sum(x => x.DurationMinutes);
                        int pausa = fatturabili.Where(x => x.TimeType == InterventionTimeType.Break).Sum(x => x.DurationMinutes);
                        int totale = lavoro + viaggio + pausa;

                        totLavoro += lavoro;
                        totViaggio += viaggio;
                        totPausa += pausa;
                        totFatturabile += totale;

                        var tecnici = intervention_Tecnici(intervento);

                        t.Cell().Element(ValueCell).Text(intervento.StartDateTime.ToString("dd/MM/yyyy")).FontSize(9);
                        t.Cell().Element(ValueCell).Text(tecnici).FontSize(9);
                        t.Cell().Element(ValueCell).AlignRight().Text(FormatMinutes(lavoro)).FontSize(9);
                        t.Cell().Element(ValueCell).AlignRight().Text(FormatMinutes(viaggio)).FontSize(9);
                        t.Cell().Element(ValueCell).AlignRight().Text(FormatMinutes(totale)).FontSize(9).SemiBold();
                    }

                    t.Cell().ColumnSpan(2).Element(TotalCell).Text("Totale").SemiBold();
                    t.Cell().Element(TotalCell).AlignRight().Text(FormatMinutes(totLavoro)).SemiBold();
                    t.Cell().Element(TotalCell).AlignRight().Text(FormatMinutes(totViaggio)).SemiBold();
                    t.Cell().Element(TotalCell).AlignRight().Text(FormatMinutes(totFatturabile)).SemiBold();

                    // La pausa fatturabile e' un'eccezione (nascono non fatturabili): si mostra
                    // solo quando c'e', per non lasciare una riga a zero in un documento cliente.
                    if (totPausa > 0)
                    {
                        t.Cell().ColumnSpan(5).Element(ValueCell)
                            .Text($"di cui pause fatturabili: {FormatMinutes(totPausa)}")
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    }
                });
            });

            static string intervention_Tecnici(TicketIntervention intervento)
            {
                var nomi = intervento.AssignedUsers?
                    .Where(u => u.User != null)
                    .Select(u => u.User.NameComplete)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList() ?? new List<string>();

                return nomi.Count > 0 ? string.Join(", ", nomi) : "-";
            }
        }

        private static void ClosureBlock(IContainer container, Ticket ticket)
        {
            // ShowEntire: senza, la tabella restava su una pagina e la descrizione di chiusura
            // finiva da sola sulla successiva, staccata dal titolo del riquadro.
            container.ShowEntire().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text("Chiusura").SemiBold();

                col.Item().PaddingTop(6).Table(t =>
                {
                    FourColumns(t);
                    Row(t, "Data chiusura", ticket.DateClosed?.ToString("dd/MM/yyyy HH:mm") ?? "", "Chiuso da", ticket.UserClosed?.NameComplete ?? "");
                });

                if (!string.IsNullOrWhiteSpace(ticket.CloseDescription))
                {
                    col.Item().PaddingTop(6).Element(ValueBox).Text(ticket.CloseDescription);
                }
            });
        }

        private static void TextAreaBlock(IContainer container, string title, string? content)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(title).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(60)
                    .Text(string.IsNullOrWhiteSpace(content) ? " " : content);
            });
        }

        private void FooterBlock(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_company?.RagioneSociale).FontSize(9);
                        c.Item().Text($"{_company?.Indirizzo} {_company?.Cap} {_company?.Citta} {_company?.Provincia} {_company?.Stato}").FontSize(9);
                        c.Item().Text($"Tel. {_company?.Telefono} / Fax {_company?.Fax}").FontSize(9);
                        c.Item().Text(_company?.Email).FontSize(9);
                    });

                    row.ConstantItem(120).AlignRight().DefaultTextStyle(x => x.FontSize(9)).Text(t =>
                    {
                        t.Span("Pagina ");
                        t.CurrentPageNumber();
                        t.Span(" di ");
                        t.TotalPages();
                    });
                });
            });
        }

        // ─── Helper ──────────────────────────────────────────────────────────────

        private static void FourColumns(TableDescriptor t)
            => t.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(110);
                cols.RelativeColumn();
                cols.ConstantColumn(95);
                cols.RelativeColumn();
            });

        private static void Row(TableDescriptor t, string l1, string v1, string l2, string v2)
        {
            t.Cell().Element(LabelCell).Text(l1);
            t.Cell().Element(ValueCell).Text(v1);
            t.Cell().Element(LabelCell).Text(l2);
            t.Cell().Element(ValueCell).Text(v2);
        }

        private static string Numero(Ticket ticket)
            => string.IsNullOrWhiteSpace(ticket.Numero) ? $"#{ticket.Id}" : ticket.Numero;

        private static string Data(DateTime? value)
            => value?.ToString("dd/MM/yyyy") ?? "";

        private static string StatoTicket(Ticket ticket)
            => !string.IsNullOrWhiteSpace(ticket.State?.Description)
                ? ticket.State!.Description
                : ticket.Closed ? "Chiuso" : "Aperto";

        private static string FormatMinutes(int totalMinutes)
            => $"{totalMinutes / 60}h {totalMinutes % 60:00}m";

        private static IContainer LabelCell(IContainer c) =>
            c.PaddingVertical(2).PaddingRight(6).AlignMiddle().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.Grey.Darken2));

        private static IContainer ValueCell(IContainer c) =>
            c.PaddingVertical(2).AlignMiddle();

        private static IContainer ValueBox(IContainer c) =>
            c.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6);

        private static IContainer HeaderCell(IContainer c) =>
            c.PaddingVertical(3).PaddingRight(4).Background(Colors.Grey.Lighten3).DefaultTextStyle(x => x.SemiBold().FontSize(9));

        private static IContainer TotalCell(IContainer c) =>
            c.PaddingVertical(3).PaddingRight(4).BorderTop(1).BorderColor(Colors.Grey.Lighten2).DefaultTextStyle(x => x.FontSize(9));
    }
}
