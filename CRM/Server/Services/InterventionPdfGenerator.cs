using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
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

        public InterventionPdfGenerator(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<byte[]> GenerateInterventionPdfAsync(int interventionId, string languageCode = "en")
        {
            // Carica le traduzioni per la lingua selezionata
            var translations = await PdfTranslations.LoadAsync(_context, languageCode);

            // Recupera l'intervento con tutte le relazioni necessarie
            var intervention = await _context.TicketsInterventions
                .Include(x => x.Ticket)
                    .ThenInclude(t => t.Company)
                .Include(x => x.User)
                .Include(x => x.TicketInterventionsTypes)
                    .ThenInclude(it => it.InterventionTypeLanguages)
                        .ThenInclude(itl => itl.Language)
                .FirstOrDefaultAsync(x => x.Id == interventionId);

            if (intervention == null)
            {
                throw new InvalidOperationException($"Intervention #{interventionId} non trovato");
            }

            // Usa le traduzioni per i tipi di intervento
            var interventionTypesList = intervention.TicketInterventionsTypes.Select(it => new InterventionTypeItem
            {
                Id = it.Id,
                Checked = true,
                Desc = GetInterventionTypeDesc(it, languageCode)
            }).ToList();

            // Recupera gli articoli dell'intervento
            var articles = await _context.TicketInterventionArticles
                .Include(x => x.Product)
                .Include(x => x.Article)
                .Where(x => x.IdTicketIntervention == interventionId)
                .ToListAsync();

            // Configura la licenza QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // Genera il documento
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(10, Unit.Millimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

                    // Header
                    page.Header()
                        .Height(35)
                        .Padding(5)
                        .Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text(translations.MinuteOfIntervention)
                                    .FontSize(11)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken2);
                            });
                        });

                    // Contenuto
                    page.Content()
                        .PaddingVertical(0.2f, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(4);

                            // Sezione Cliente
                            column.Item().Element(c => RenderClientSection(c, intervention.Ticket.Company, translations));

                            // Sezione Tecnico
                            column.Item().Element(c => RenderTechnicianSection(c, intervention.User, translations));

                            // Sezione Tipi di Intervento
                            column.Item().Element(c => RenderInterventionTypes(c, interventionTypesList));

                            // Sezione Macchine/Dispositivi
                            if (articles.Any())
                            {
                                column.Item().Element(c => RenderMachinesSection(c, articles, translations));
                            }

                            // Sezione Descrizione Intervento
                            column.Item().Element(c => RenderInterventionDetails(c, intervention, translations));

                            // Sezione Attività
                            if (!string.IsNullOrWhiteSpace(intervention.Activities))
                            {
                                column.Item().Element(c => RenderTextArea(c, 
                                    translations.Activities, 
                                    intervention.Activities));
                            }

                            // Dichiarazione Cliente
                            column.Item().Element(c => RenderCustomerDeclaration(c, translations));

                            // Parti Sostituite
                            if (!string.IsNullOrWhiteSpace(intervention.MountedParts))
                            {
                                column.Item().Element(c => RenderTextArea(c,
                                    translations.ReplacedParts,
                                    intervention.MountedParts));
                            }

                            // Note
                            if (!string.IsNullOrWhiteSpace(intervention.Note))
                            {
                                column.Item().Element(c => RenderTextArea(c,
                                    translations.Notes,
                                    intervention.Note));
                            }

                            // Firma
                            column.Item().Element(c => RenderSignatureSection(c, translations, intervention.CustomerSignature, intervention.SignatureDate, intervention.SignatureName, intervention.SignatureStatus));
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(style => style.FontSize(7))
                        .Text(x =>
                        {
                            x.Span($"{translations.Page} ");        
                            x.CurrentPageNumber();
                            x.Span($" {translations.Of} ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private static void RenderClientSection(IContainer container, Company company, PdfTranslations t)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(column =>
            {
                column.Item().PaddingBottom(2).Text(t.Client)
                    .FontSize(9)
                    .Bold()
                    .FontColor(Colors.Blue.Darken1);

                RenderField(column, t.CompanyName, company.RagioneSociale);
                RenderField(column, t.Address, company.Indirizzo);
                RenderField(column, t.Zip, company.Cap);
                RenderField(column, t.City, company.Citta);
                RenderField(column, t.Country, company.Stato);
                RenderField(column, t.VatId, company.PIva);
            });
        }

        private static void RenderTechnicianSection(IContainer container, ApplicationUser user, PdfTranslations t)
        {
            container.Padding(3).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span(t.OurTechnician + " ")
                        .FontSize(8);
                    
                    text.Span(user.NameComplete ?? "N/A")
                        .FontSize(8)
                        .Bold()
                        .Underline();
                    
                    text.Span(t.HasIntervenedFor)
                        .FontSize(8);
                });
            });
        }

        private static void RenderInterventionTypes(IContainer container, List<InterventionTypeItem> types)
        {
            container.Padding(4).Column(column =>
            {
                column.Spacing(3);

                foreach (var type in types)
                {
                    column.Item().Row(row =>
                    {
                        row.ConstantItem(18).AlignMiddle().Text(type.Checked ? "☑" : "☐")
                            .FontSize(10);

                        row.RelativeItem().AlignMiddle().Text(type.Desc)
                            .FontSize(10);
                    });
                }
            });
        }

        private static void RenderMachinesSection(IContainer container, List<TicketInterventionArticle> articles, PdfTranslations t)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(column =>
            {
                column.Item().PaddingBottom(2).Row(row =>
                {
                    row.RelativeItem().Text(t.MachinesDevices)
                        .FontSize(9)
                        .Bold();
                });

                foreach (var article in articles)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(3).Text($"{t.Model}: {article.Product?.Name ?? ""}")
                            .FontSize(7);

                        row.RelativeItem(3).Text($"{t.SerialNumber}: {article.Article?.SerialNumber ?? ""}")
                            .FontSize(7);

                        row.RelativeItem(2).Text($"{t.Year}: {article.Article?.DeliveryDate?.ToString("d")}")
                            .FontSize(7);
                    });
                }
            });
        }

        private static void RenderInterventionDetails(IContainer container, TicketIntervention intervention, PdfTranslations t)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(column =>
            {
                column.Item().PaddingBottom(2).Row(row =>
                {
                    row.RelativeItem().Text(t.ServiceTimePeriod)
                        .FontSize(9)
                        .Bold();
                });

                RenderField(column, t.ServiceBegan, 
                    intervention.StartDateTime.ToString("g"));

                RenderField(column, t.ServiceEnded, 
                    intervention.EndDateTime.ToString("g"));
            });
        }

        private static void RenderCustomerDeclaration(IContainer container, PdfTranslations t)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(column =>
            {
                column.Item().PaddingBottom(2).Row(row =>
                {
                    row.RelativeItem().AlignLeft().Text(t.CustomerDeclaration)
                        .FontSize(10)
                        .Bold();
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(t.Declaration1)
                            .FontSize(8);
                        col.Item().Text(t.Declaration2)
                            .FontSize(8);
                        col.Item().Text(t.Declaration3)
                            .FontSize(8);
                        col.Item().Text(t.Declaration4)
                            .FontSize(8);
                    });
                });
            });
        }

        private static void RenderTextArea(IContainer container, string title, string content)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(column => // ✅ RIDOTTO: da 10 a 5
            {
                column.Item().PaddingBottom(2).Text(title) // ✅ RIDOTTO: da 5 a 2
                    .FontSize(9) // ✅ RIDOTTO: da 11 a 9
                    .Bold();

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten3)
                    .Padding(4) // ✅ RIDOTTO: da 8 a 4
                    .MinHeight(50) // ✅ RIDOTTO: da 100 a 50
                    .Text(content ?? "")
                    .FontSize(7) // ✅ RIDOTTO: da 9 a 7
                    .LineHeight(1.2f); // ✅ RIDOTTO: da 1.5 a 1.2
            });
        }

        private static void RenderSignatureSection(IContainer container, PdfTranslations t, string? customerSignatureBase64, DateTime? signatureDate, string? signerName, SignatureStatus? signatureStatus)
        {
            container.Padding(5).Column(column =>
            {
                column.Spacing(8);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"{t.AcceptedOn} {signatureDate:d}")
                            .FontSize(8);
                    });

                    row.RelativeItem();
                });

                column.Item().Row(row =>
                {
                    row.RelativeItem();

                    row.RelativeItem().Column(col =>
                    {
                        // Se c'è una firma digitale, mostrala
                        if (!string.IsNullOrWhiteSpace(customerSignatureBase64))
                        {
                            try
                            {
                                // Converte da Base64 a byte array
                                byte[] signatureBytes = Convert.FromBase64String(customerSignatureBase64);
                                
                                Console.WriteLine($"PDF: Firma trovata, lunghezza: {signatureBytes.Length} bytes");

                                if (signatureBytes.Length > 8)
                                {
                                    // PNG header signature: 89 50 4E 47 0D 0A 1A 0A
                                    bool isPng = signatureBytes[0] == 0x89 && 
                                                 signatureBytes[1] == 0x50 && 
                                                 signatureBytes[2] == 0x4E && 
                                                 signatureBytes[3] == 0x47;

                                    if (isPng)
                                    {
                                        Console.WriteLine("PDF: Formato PNG valido, rendering immagine...");
                                        
                                        try
                                        {
                                            // ✅ FIRMA DIGITALE: Mostra immagine centrata
                                            col.Item()
                                                .AlignCenter()
                                                .PaddingVertical(10)
                                                .Image(signatureBytes, ImageScaling.FitArea);
                                            
                                            Console.WriteLine("PDF: Immagine renderizzata con successo");
                                        }
                                        catch (Exception imgEx)
                                        {
                                            Console.WriteLine($"PDF: Errore rendering QuestPDF - {imgEx.Message}");
                                            
                                            // ✅ FALLBACK: Mostra box con testo
                                            col.Item()
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
                                        Console.WriteLine($"PDF: Formato immagine non riconosciuto. Header: {BitConverter.ToString(signatureBytes.Take(8).ToArray())}");
                                        
                                        col.Item().AlignCenter()
                                            .BorderBottom(1)
                                            .BorderColor(Colors.Grey.Medium)
                                            .PaddingBottom(20)
                                            .Text("[Firma presente ma formato non valido]")
                                            .FontSize(7)
                                            .Italic();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"PDF: Firma troppo piccola ({signatureBytes.Length} bytes)");
                                    ShowEmptySignatureSpace(col);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"PDF: Errore conversione firma - {ex.Message}");
                                
                                col.Item().AlignCenter()
                                    .BorderBottom(1)
                                    .BorderColor(Colors.Red.Lighten2)
                                    .PaddingBottom(20)
                                    .Text($"[Errore caricamento firma: {ex.Message}]")
                                    .FontSize(7)
                                    .Italic()
                                    .FontColor(Colors.Red.Darken1);
                            }
                        }
                        else
                        {
                            Console.WriteLine("PDF: Nessuna firma presente");
                            ShowEmptySignatureSpace(col);
                        }

                        col.Item().AlignCenter().Text(t.StampSignature)
                            .FontSize(7)
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);

                        // ✅ NOME FIRMATARIO: Mostra chi ha firmato
                        if (!string.IsNullOrWhiteSpace(signerName))
                        {
                            col.Item().AlignCenter()
                                .PaddingTop(2)
                                .Text(signerName)
                                .FontSize(8)
                                .Bold()
                                .FontColor(Colors.Blue.Darken1);
                        }

                        // ✅ TIMESTAMP FIRMA: Mostra data e ora se presente
                        if (signatureDate.HasValue)
                        {
                            col.Item().AlignCenter()
                                .PaddingTop(3)
                                .Text($"{t.SignedOn}: {signatureDate.Value:g}")
                                .FontSize(6)
                                .FontColor(Colors.Grey.Darken2);
                        }

                        // ✅ STATO FIRMA: Pending/Verified
                        if (signatureStatus.HasValue)
                        {
                            var statusText = signatureStatus.Value switch
                            {
                                SignatureStatus.Pending => "⏳ In attesa di conferma",
                                SignatureStatus.Verified => "✅ Firma verificata",
                                SignatureStatus.Rejected => "❌ Firma rifiutata",
                                _ => ""
                            };

                            var statusColor = signatureStatus.Value switch
                            {
                                SignatureStatus.Verified => Colors.Green.Darken1,
                                SignatureStatus.Rejected => Colors.Red.Darken1,
                                _ => Colors.Orange.Darken1
                            };

                            col.Item().AlignCenter()
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

        /// <summary>
        /// Mostra spazio vuoto per firma manuale
        /// </summary>
        private static void ShowEmptySignatureSpace(ColumnDescriptor col)
        {
            col.Item().AlignCenter()
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Medium)
                .PaddingBottom(20)
                .Text("");
        }

        private static void RenderField(ColumnDescriptor column, string label, string? value)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(140).Text($"{label}:") // ✅ RIDOTTO: da 180 a 140
                    .FontSize(7) // ✅ RIDOTTO: da 9 a 7
                    .FontColor(Colors.Grey.Darken1);

                row.RelativeItem().Text(value ?? "N/A")
                    .FontSize(7); // ✅ RIDOTTO: da 9 a 7
            });
        }

        private string GetInterventionTypeDesc(InterventionType interventionType, string languageCode)
        {
            // Controlla se ci sono traduzioni
            if (interventionType.InterventionTypeLanguages == null || !interventionType.InterventionTypeLanguages.Any())
            {
                return interventionType.Name ?? "N/A";
            }

            // Cerca la traduzione nella lingua richiesta
            var translation = interventionType.InterventionTypeLanguages
                .FirstOrDefault(x => x.Language?.LanguageCode == languageCode);

            if (translation != null)
            {
                return translation.Name ?? interventionType.Name ?? "N/A";
            }

            // Fallback: prende la prima traduzione disponibile
            var firstTranslation = interventionType.InterventionTypeLanguages
                .OrderBy(x => x.Language?.Index ?? 99)
                .FirstOrDefault();

            return firstTranslation?.Name ?? interventionType.Name ?? "N/A";
        }
    }

    // Helper class per intervention types
    internal class InterventionTypeItem
    {
        public int Id { get; set; }
        public bool Checked { get; set; }
        public string Desc { get; set; } = string.Empty;
    }

    /// <summary>
    /// Classe per gestire le traduzioni delle label del PDF
    /// </summary>
    internal class PdfTranslations
    {
        public string MinuteOfIntervention { get; set; } = "Minute of Intervention";
        public string Client { get; set; } = "Client:";
        public string CompanyName { get; set; } = "Company name";
        public string Address { get; set; } = "Address";
        public string Zip { get; set; } = "ZIP";
        public string City { get; set; } = "City";
        public string Country { get; set; } = "Country";
        public string VatId { get; set; } = "VAT Id.";
        public string OurTechnician { get; set; } = "Our Technician, Mr.";
        public string HasIntervenedFor { get; set; } = " has intervened at your premises for:";
        public string MachinesDevices { get; set; } = "Machines / Devices:";
        public string Model { get; set; } = "Model";
        public string SerialNumber { get; set; } = "S/N";
        public string Year { get; set; } = "Year";
        public string ServiceTimePeriod { get; set; } = "Service Time Period";
        public string ServiceBegan { get; set; } = "Service began";
        public string ServiceEnded { get; set; } = "Service ended on";
        public string Activities { get; set; } = "Activities:";
        public string CustomerDeclaration { get; set; } = "At the end of the intervention, the customer declares:";
        public string Declaration1 { get; set; } = "1. That the functions of the machines / devices are all working;";
        public string Declaration2 { get; set; } = "2. That the operation of the same is regular;";
        public string Declaration3 { get; set; } = "3. That security guards are operational;";
        public string Declaration4 { get; set; } = "4. That in view of the above, nothing shall prevent the payment.";
        public string ReplacedParts { get; set; } = "Replaced and/or Mounted Parts";
        public string Notes { get; set; } = "Notes and/or reserves";
        public string AcceptedOn { get; set; } = "Accepted on:";
        public string StampSignature { get; set; } = "Stamp & Signature";
        public string SignedOn { get; set; } = "Digitally signed on";
        public string Page { get; set; } = "Page";
        public string Of { get; set; } = "of";

        public static async Task<PdfTranslations> LoadAsync(ApplicationDbContext context, string languageCode)
        {
            var translations = new PdfTranslations();

            // Trova la lingua nel database
            var language = await context.Languages
                .FirstOrDefaultAsync(x => x.LanguageCode == languageCode);

            if (language == null)
            {
                // Lingua non trovata, usa default inglese
                return translations;
            }

            // Qui potresti caricare traduzioni dal database se le hai in una tabella
            // Per ora uso un dizionario hardcoded con le traduzioni principali
            var translationMap = GetTranslationMap(languageCode);
            
            if (translationMap != null)
            {
                ApplyTranslations(translations, translationMap);
            }

            return translations;
        }

        private static Dictionary<string, string>? GetTranslationMap(string languageCode)
        {
            // Usa solo inglese per ora - le altre lingue vanno aggiunte manualmente
            // creando file .resx specifici (es. PdfLabels.it.resx, PdfLabels.fr.resx)
            return null;
        }

        private static void ApplyTranslations(PdfTranslations target, Dictionary<string, string> map)
        {
            foreach (var kvp in map)
            {
                var prop = typeof(PdfTranslations).GetProperty(kvp.Key);
                prop?.SetValue(target, kvp.Value);
            }
        }
    }
}
