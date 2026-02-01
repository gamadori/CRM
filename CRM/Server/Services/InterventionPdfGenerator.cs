using AspNetCoreGeneratedDocument;
using CRM.Server.Data;
using CRM.Server.Reports.Pdf;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Services
{
    /// <summary>
    /// Servizio per generare PDF di interventi con QuestPDF
    /// </summary>
    public interface IInterventionPdfGenerator
    {
        Task<byte[]> GenerateInterventionPdfAsync(int interventionId, string languageCode = "en");
    }

    public class InterventionPdfGenerator : IInterventionPdfGenerator
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizerFactory _localizerFactory;

        private Company? _company;
        private int? _logoId = null;

        public InterventionPdfGenerator(
            ApplicationDbContext context,
            IStringLocalizerFactory localizerFactory)
        {
            _context = context;
            _localizerFactory = localizerFactory;
        }

        public async Task<byte[]> GenerateInterventionPdfAsync(int interventionId, string languageCode = "en")
        {
            var logFile = Path.Combine(Path.GetTempPath(), $"pdf_generation_{interventionId}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            
            void Log(string message)
            {
                var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                Console.WriteLine(logMessage);
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            
            Log($"========== INIZIO GENERAZIONE PDF #{interventionId} ==========");
            Log($"File log: {logFile}");
            Log($"Lingua richiesta: {languageCode}");
            
            try
            {
                var settings = await _context.GlobalSettings.FirstOrDefaultAsync();
                Log($"Settings caricati: {settings != null}");
                
                if (settings != null)
                {
                    _logoId = settings.LogoReport;
                    _company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == settings.IdHeadQuarter);
                    Log($"LogoId: {_logoId}, Company: {_company?.RagioneSociale ?? "NULL"}");
                }
                
                Log($"Inizio caricamento labels...");
                // Carica le traduzioni per la lingua selezionata
                var labels = await LoadLabelsAsync(_context, languageCode);
                Log($"Labels caricati - MinuteOfIntervention: '{labels.MinuteOfIntervention}'");

                Log($"Caricamento intervention dal database...");

                // Recupera l'intervento con tutte le relazioni necessarie
                var intervention = await _context.TicketsInterventions
                    .Include(x => x.Ticket)
                        .ThenInclude(t => t.Company)
                    .Include(x => x.User)
                    .Include(x => x.TicketInterventionsTypes)
                        .ThenInclude(it => it.InterventionTypeLanguages)
                            .ThenInclude(itl => itl.Language)
                    .Include(x => x.TicketInterventionArticles)
                        .ThenInclude(a => a.Product)
                    .Include(x => x.TicketInterventionArticles)
                        .ThenInclude(a => a.Article)
                    .FirstOrDefaultAsync(x => x.Id == interventionId);

                if (intervention == null)
                {
                    Log($"ERRORE: Intervention #{interventionId} NON TROVATO!");
                    throw new InvalidOperationException($"Intervention #{interventionId} non trovato");
                }
                
                Log($"Intervention caricato: Ticket #{intervention.IdTicket}, User: {intervention.User?.NameComplete}");
                // Configura la licenza QuestPDF
                QuestPDF.Settings.License = LicenseType.Community;

                Log($"Inizio generazione documento QuestPDF...");
                // Genera il documento
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        ConfigurePage(page);

                        page.Header().Element(c => HeaderBlock(c, labels));

                        page.Content().PaddingTop(10).Column(col =>
                        {
                            col.Item().Element(c => TitleBlock(c, labels.MinuteOfIntervention));

                            col.Item().PaddingTop(8).Element(c => ClientBlock(c, intervention.Ticket.Company, labels));

                            col.Item().PaddingTop(10).Element(c => TechnicianAndPurposeBlock(c, intervention, labels));

                            if (intervention.TicketInterventionArticles != null && intervention.TicketInterventionArticles.Any())
                            {
                                col.Item().PaddingTop(10).Element(c => MachinesBlock(c, intervention.TicketInterventionArticles.ToList(), labels));
                            }

                            col.Item().PaddingTop(10).Element(c => ServiceTimesBlock(c, intervention, labels));

                            if (!string.IsNullOrWhiteSpace(intervention.Activities))
                            {
                                col.Item().PaddingTop(10).Element(c => ActivitiesBlock(c, intervention.Activities, labels));
                            }

                            col.Item().PaddingTop(10).Element(c => CustomerDeclarationBlock(c, labels));
                        });

                        page.Footer().Element(c => FooterBlock(c, labels));
                    });

                    // Pagina 2: Parti sostituite, Note e Firma
                    container.Page(page =>
                    {
                        ConfigurePage(page);

                        page.Header().Element(c => HeaderBlock(c, labels));

                        page.Content().PaddingTop(10).Column(col =>
                        {
                            col.Item().Element(c => TitleBlock(c, labels.ReplacedAndMountedParts));

                            // Mostra sempre la sezione con almeno 2 righe vuote se non ci sono dati
                            col.Item().PaddingTop(8).Element(c => TextAreaBlock(c, labels.ReplacedAndMountedParts, intervention.MountedParts));

                            if (!string.IsNullOrWhiteSpace(intervention.Note))
                            {
                                col.Item().PaddingTop(12).Element(c => NotesBlock(c, intervention.Note, labels));
                            }

                            col.Item().PaddingTop(12).Element(c => RenderSignatureSection(c, labels, intervention.CustomerSignature, intervention.SignatureDate, intervention.SignatureName, intervention.SignatureStatus));
                        });

                        page.Footer().Element(c => FooterBlock(c, labels));
                    });
                });

                Log($"Generazione bytes PDF...");
                var pdfBytes = document.GeneratePdf();
                Log($"========== PDF GENERATO CON SUCCESSO: {pdfBytes.Length} bytes ==========");
                
                return pdfBytes;
            }
            catch (Exception ex)
            {
                Log($"========== ERRORE FATALE ==========");
                Log($"Exception Type: {ex.GetType().Name}");
                Log($"Message: {ex.Message}");
                Log($"StackTrace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Log($"InnerException: {ex.InnerException.Message}");
                    Log($"InnerException StackTrace: {ex.InnerException.StackTrace}");
                }
                
                throw;
            }
        }

        private  void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(35);
            page.DefaultTextStyle(x => x.FontSize(10));
        }

        private void HeaderBlock(IContainer container, InterventionReportLabels labels)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(8).Row(row =>
                {
                    // Company Info (left side)
                    row.RelativeItem().Column(col =>
                    {
                        if (_company != null)
                        {
                            col.Item().Text(_company.RagioneSociale).SemiBold().FontSize(12);
                            col.Item().Text($"{_company.Indirizzo} {_company.Cap} {_company.Citta} {_company.Stato}");
                            col.Item().Text($"{_company.Telefono} {_company.Fax}");
                            col.Item().Text($"{_company.Email} | {_company.Web}");
                            col.Item().Text($"VAT {_company.PIva}").FontColor(Colors.Grey.Darken1).FontSize(9);
                        }
                    });

                    // Logo (right side)
                    if (_logoId.HasValue)
                    {
                        row.ConstantItem(120).AlignRight().Element(c => RenderLogo(c));
                    }
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
        }

        private void RenderLogo(IContainer container)
        {
            try
            {
                // Carica il logo dal database
                var logo = _context.Logos.FirstOrDefault(l => l.Id == _logoId.Value);
                
                if (logo != null && !string.IsNullOrWhiteSpace(logo.InputFile))
                {
                    // Rimuovi il prefisso data:image/xxx;base64, se presente
                    string base64Image = logo.InputFile;
                    
                    if (base64Image.Contains(","))
                    {
                        base64Image = base64Image.Split(',')[1];
                    }

                    // Converti Base64 in byte array
                    byte[] imageBytes = Convert.FromBase64String(base64Image);

                    // Verifica che sia un'immagine valida (PNG/JPEG)
                    if (imageBytes.Length > 8)
                    {
                        bool isPng = imageBytes[0] == 0x89 && 
                                     imageBytes[1] == 0x50 && 
                                     imageBytes[2] == 0x4E && 
                                     imageBytes[3] == 0x47;
                        
                        bool isJpeg = imageBytes[0] == 0xFF && 
                                      imageBytes[1] == 0xD8;

                        if (isPng || isJpeg)
                        {
                            container
                                .Width(120)
                                .Height(60)
                                .Image(imageBytes, ImageScaling.FitArea);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error silently - don't break PDF generation
                Console.WriteLine($"Errore caricamento logo: {ex.Message}");
            }
        }

        private static void TitleBlock(IContainer container, string title)
        {
            container
                .PaddingVertical(6)
                .Background(Colors.Grey.Lighten4)
                .Border(1).BorderColor(Colors.Grey.Lighten2)
                .AlignCenter()
                .Text(title).SemiBold().FontSize(14);
        }

        private static void ClientBlock(IContainer container, Company company, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.Client).SemiBold();

                col.Item().PaddingTop(6).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(120);
                        cols.RelativeColumn();
                        cols.ConstantColumn(80);
                        cols.RelativeColumn();
                    });

                    Row(t, labels.CompanyName, company.RagioneSociale ?? "", labels.VatId, company.PIva ?? "");
                    Row(t, labels.Address, company.Indirizzo ?? "", labels.Zip, company.Cap ?? "");
                    Row(t, labels.City, company.Citta ?? "", labels.Country, company.Stato ?? "");
                });
            });

            static void Row(TableDescriptor t, string l1, string v1, string l2, string v2)
            {
                t.Cell().Element(LabelCell).Text(l1);
                t.Cell().Element(ValueCell).Text(v1);
                t.Cell().Element(LabelCell).Text(l2);
                t.Cell().Element(ValueCell).Text(v2);
            }
        }

        private static void TechnicianAndPurposeBlock(IContainer container, TicketIntervention intervention, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text($"{labels.OurTechnician} {intervention.User?.NameComplete ?? "N/A"}").SemiBold();
                
                col.Item().PaddingTop(2).Text($"{labels.HasIntervenedAt} {intervention.Ticket?.Company?.RagioneSociale ?? "your premises"} {labels.For} ");

                col.Item().PaddingTop(8).Column(c =>
                {
                    if (intervention.TicketInterventionsTypes != null && intervention.TicketInterventionsTypes.Any())
                    {
                        foreach (var interventionType in intervention.TicketInterventionsTypes)
                        {
                            var translatedName = GetInterventionTypeTranslation(interventionType, labels.CultureCode);
                            CheckboxLine(c, true, translatedName);
                        }
                    }
                });
            });
        }

        private static void MachinesBlock(IContainer container, List<TicketInterventionArticle> articles, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.MachinesDevices).SemiBold();

                col.Item().PaddingTop(4).Column(c =>
                {
                    foreach (var article in articles)
                    {
                        c.Item().PaddingVertical(2).Row(row =>
                        {
                            row.RelativeItem().Text($"• {article.Product?.Name ?? "N/A"} - S/N: {article.Article?.SerialNumber ?? "N/A"}");
                        });
                    }
                });
            });
        }

        private static void ServiceTimesBlock(IContainer container, TicketIntervention intervention, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(180);
                    cols.RelativeColumn();
                    cols.ConstantColumn(180);
                    cols.RelativeColumn();
                });

                t.Cell().Element(LabelCell).Text(labels.ServiceBegan);
                t.Cell().Element(ValueCell).Text(intervention.StartDateTime.ToString("g"));
                t.Cell().Element(LabelCell).Text(labels.ServiceEnded);
                t.Cell().Element(ValueCell).Text(intervention.EndDateTime.ToString("g"));
            });
        }

        private static void ActivitiesBlock(IContainer container, string activities, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.Activities).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(70)
                    .Text(activities);
            });
        }

        private static void CustomerDeclarationBlock(IContainer container, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.CustomerDeclaration).SemiBold();

                col.Item().PaddingTop(6).Text(labels.Declaration1);
                col.Item().Text(labels.Declaration2);
                col.Item().Text(labels.Declaration3);
                col.Item().Text(labels.Declaration4);
            });
        }

        private static void TextAreaBlock(IContainer container, string title, string content)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(title).SemiBold();
                
                // Se il contenuto è vuoto, mostra almeno 2 righe vuote con MinHeight maggiore
                if (string.IsNullOrWhiteSpace(content))
                {
                    col.Item().PaddingTop(4).Element(ValueBox).MinHeight(50)
                        .Text(" \n \n ");
                }
                else
                {
                    col.Item().PaddingTop(4).Element(ValueBox).MinHeight(50)
                        .Text(content);
                }
            });
        }

        private static void NotesBlock(IContainer container, string notes, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.NotesOrReserves).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(90)
                    .Text(notes);

                col.Item().PaddingTop(6).DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken1)).Row(row =>
                {
                    row.ConstantItem(12).Text("☐");
                    row.RelativeItem().Text(labels.TickIfNoComments);
                });
            });
        }

        private static void RenderSignatureSection(IContainer container, InterventionReportLabels labels, string? customerSignatureBase64, DateTime? signatureDate, string? signerName, SignatureStatus? signatureStatus)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.AcceptedOn).SemiBold();
                col.Item().Text(signatureDate?.ToString("MM/dd/yyyy") ?? DateTime.Now.ToString("MM/dd/yyyy"));

                col.Item().PaddingTop(20).Row(row =>
                {
                    row.RelativeItem();

                    row.RelativeItem().Column(signatureCol =>
                    {
                        // Se c'è una firma digitale, mostramala
                        if (!string.IsNullOrWhiteSpace(customerSignatureBase64))
                        {
                            try
                            {
                                byte[] signatureBytes = Convert.FromBase64String(customerSignatureBase64);

                                if (signatureBytes.Length > 8)
                                {
                                    bool isPng = signatureBytes[0] == 0x89 && 
                                                 signatureBytes[1] == 0x50 && 
                                                 signatureBytes[2] == 0x4E && 
                                                 signatureBytes[3] == 0x47;

                                    if (isPng)
                                    {
                                        try
                                        {
                                            signatureCol.Item()
                                                .AlignCenter()
                                                .PaddingVertical(10)
                                                .Image(signatureBytes, ImageScaling.FitArea);
                                        }
                                        catch
                                        {
                                            signatureCol.Item()
                                                .AlignCenter()
                                                .PaddingVertical(10)
                                                .Border(1)
                                                .BorderColor(Colors.Blue.Lighten2)
                                                .Background(Colors.Grey.Lighten4)
                                                .Padding(20)
                                                .Text("✓ Digitally Signed")
                                                .FontSize(12)
                                                .Bold()
                                                .FontColor(Colors.Blue.Darken1);
                                        }
                                    }
                                    else
                                    {
                                        ShowEmptySignatureSpace(signatureCol);
                                    }
                                }
                                else
                                {
                                    ShowEmptySignatureSpace(signatureCol);
                                }
                            }
                            catch
                            {
                                ShowEmptySignatureSpace(signatureCol);
                            }
                        }
                        else
                        {
                            ShowEmptySignatureSpace(signatureCol);
                        }

                        signatureCol.Item().AlignCenter().Text("Stamp & Signature")
                            .FontSize(7)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);

                        if (!string.IsNullOrWhiteSpace(signerName))
                        {
                            signatureCol.Item().AlignCenter()
                                .PaddingTop(2)
                                .Text(signerName)
                                .FontSize(8)
                                .Bold()
                                .FontColor(Colors.Blue.Darken1);
                        }

                        if (signatureDate.HasValue)
                        {
                            signatureCol.Item().AlignCenter()
                                .PaddingTop(3)
                                .Text($"Signed on: {signatureDate.Value:g}")
                                .FontSize(6)
                                .FontColor(Colors.Grey.Darken2);
                        }

                        if (signatureStatus.HasValue)
                        {
                            var statusText = signatureStatus.Value switch
                            {
                                SignatureStatus.Pending => "⏳ Pending confirmation",
                                SignatureStatus.Verified => "✅ Verified signature",
                                SignatureStatus.Rejected => "❌ Rejected signature",
                                _ => ""
                            };

                            var statusColor = signatureStatus.Value switch
                            {
                                SignatureStatus.Verified => Colors.Green.Darken1,
                                SignatureStatus.Rejected => Colors.Red.Darken1,
                                _ => Colors.Orange.Darken1
                            };

                            signatureCol.Item().AlignCenter()
                                .PaddingTop(2)
                                .Text(statusText)
                                .FontSize(6)
                                .FontColor(statusColor);
                        }
                    });

                    row.RelativeItem();
                });
            });
        }

        private static void ShowEmptySignatureSpace(ColumnDescriptor col)
        {
            col.Item().AlignCenter()
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Medium)
                .PaddingBottom(20)
                .Text("");
        }

        private  void FooterBlock(IContainer container, InterventionReportLabels labels)
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
                        t.Span($"{labels.Page} ");
                        t.CurrentPageNumber();
                        t.Span($" {labels.Of} ");
                        t.TotalPages();
                    });
                });
            });
        }

        private static void CheckboxLine(ColumnDescriptor col, bool isChecked, string label)
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(14).Text(isChecked ? "☑" : "☐");
                r.RelativeItem().Text(label);
            });
        }

        private static IContainer LabelCell(IContainer c) =>
            c.PaddingVertical(2).PaddingRight(6).AlignMiddle().DefaultTextStyle(x => x.SemiBold().FontColor(Colors.Grey.Darken2));

        private static IContainer ValueCell(IContainer c) =>
            c.PaddingVertical(2).AlignMiddle();

        private static IContainer ValueBox(IContainer c) =>
            c.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6);

        private static string GetInterventionTypeTranslation(InterventionType interventionType, string? cultureCode)
        {
            if (interventionType.InterventionTypeLanguages == null || !interventionType.InterventionTypeLanguages.Any())
            {
                return interventionType.Name ?? "N/A";
            }

            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                return interventionType.Name ?? "N/A";
            }

            var languageCode = cultureCode.Split('-')[0];

            var translation = interventionType.InterventionTypeLanguages
                .FirstOrDefault(x => x.Language != null && 
                                    (x.Language.LanguageCode == languageCode || 
                                     x.Language.LanguageCode == cultureCode));

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                return translation.Name;
            }

            var firstTranslation = interventionType.InterventionTypeLanguages
                .Where(x => x.Language != null && !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Language?.Index ?? 99)
                .FirstOrDefault();

            return firstTranslation?.Name ?? interventionType.Name ?? "N/A";
        }

        private async Task<InterventionReportLabels> LoadLabelsAsync(ApplicationDbContext context, string languageCode)
        {
            Console.WriteLine($"[PDF] LoadLabelsAsync - Lingua richiesta: {languageCode}");
            
            var culture = new System.Globalization.CultureInfo(languageCode);
            
            // Usa direttamente il ResourceManager della classe App
            var resourceManager = CRM.Shared.Resources.App.ResourceManager;
            
            Console.WriteLine($"[PDF] Caricamento traduzioni con cultura: {culture.Name}");

            var labels = new InterventionReportLabels
            {
                CultureCode = languageCode,
                
                // Usa GetString con la cultura specifica
                MinuteOfIntervention = resourceManager.GetString("Minute of Intervention", culture) ?? "Minute of Intervention",
                Client = resourceManager.GetString("Client", culture) ?? "Client",
                CompanyName = resourceManager.GetString("Company Name", culture) ?? "Company Name",
                VatId = resourceManager.GetString("VAT ID", culture) ?? "VAT ID",
                Address = resourceManager.GetString("Address", culture) ?? "Address",
                Zip = resourceManager.GetString("ZIP", culture) ?? "ZIP",
                City = resourceManager.GetString("City", culture) ?? "City",
                Country = resourceManager.GetString("Country", culture) ?? "Country",
                OurTechnician = resourceManager.GetString("Our technician", culture) ?? "Our technician",
                HasIntervenedAt = resourceManager.GetString("has intervened at", culture) ?? "has intervened at",
                For = resourceManager.GetString("for:", culture) ?? "for:",
                MachinesDevices = resourceManager.GetString("Machines/Devices", culture) ?? "Machines/Devices",
                ServiceBegan = resourceManager.GetString("Service began", culture) ?? "Service began",
                ServiceEnded = resourceManager.GetString("Service ended", culture) ?? "Service ended",
                Activities = resourceManager.GetString("Activities", culture) ?? "Activities",
                CustomerDeclaration = resourceManager.GetString("Customer Declaration", culture) ?? "Customer Declaration",
                Declaration1 = resourceManager.GetString("The undersigned declares...", culture) ?? "The undersigned declares...",
                Declaration2 = resourceManager.GetString("The customer declares...", culture) ?? "The customer declares...",
                Declaration3 = resourceManager.GetString("Any reserves must be...", culture) ?? "Any reserves must be...",
                Declaration4 = resourceManager.GetString("The customer authorizes...", culture) ?? "The customer authorizes...",
                ReplacedAndMountedParts = resourceManager.GetString("Replaced and Mounted Parts", culture) ?? "Replaced and Mounted Parts",
                NotesOrReserves = resourceManager.GetString("Notes or Reserves", culture) ?? "Notes or Reserves",
                TickIfNoComments = resourceManager.GetString("Tick if no comments", culture) ?? "Tick if no comments",
                AcceptedOn = resourceManager.GetString("Accepted on", culture) ?? "Accepted on",
                Page = resourceManager.GetString("Page", culture) ?? "Page",
                Of = resourceManager.GetString("of", culture) ?? "of"
            };

            Console.WriteLine($"[PDF] Labels caricati - MinuteOfIntervention: '{labels.MinuteOfIntervention}'");

            return await Task.FromResult(labels);
        }
    }
}
