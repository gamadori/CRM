using CRM.Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Reports.Pdf
{
    // NuGet: QuestPDF (latest)
    // using QuestPDF.Fluent;
    // using QuestPDF.Helpers;
    // using QuestPDF.Infrastructure;

    /// <summary>
    /// Classe per gestire le traduzioni delle label del PDF
    /// </summary>
    public class InterventionReportLabels
    {
        public string? CultureCode { get; set; } = "en-US"; // e.g. "it-IT" for Italian
        public string MinuteOfIntervention { get; set; } = "MINUTE OF INTERVENTION";
        public string Client { get; set; } = "Client";
        public string CompanyName { get; set; } = "Company name:";
        public string Address { get; set; } = "Address:";
        public string Zip { get; set; } = "ZIP:";
        public string City { get; set; } = "City:";
        public string Country { get; set; } = "Country:";
        public string VatId { get; set; } = "VAT Id:";
        public string OurTechnician { get; set; } = "Our Technician, Mr. ";
        public string HasIntervenedAt { get; set; } = "has intervened at ";
        public string For { get; set; } = " for:";
        public string ToInstall { get; set; } = "To Install";
        public string ToTest { get; set; } = "To Test";
        public string ToCheck { get; set; } = "To Check";
        public string ToRepair { get; set; } = "To Repair";
        public string ToSuggestRecommend { get; set; } = "To Suggest / Recommend";
        public string DevelopmentAndDesign { get; set; } = "Development and Design";
        public string MachinesDevices { get; set; } = "Machines / Devices";
        public string DescriptionOfIntervention { get; set; } = "Description of Intervention";
        public string ServiceBegan { get; set; } = "Service began:";
        public string ServiceEnded { get; set; } = "Service ended on:";
        public string Activities { get; set; } = "Activities";
        public string CustomerDeclaration { get; set; } = "At the end of the intervention, the customer declares:";
        public string Declaration1 { get; set; } = "1. That the functions of the machines / devices are all working;";
        public string Declaration2 { get; set; } = "2. That the operation of the same is regular;";
        public string Declaration3 { get; set; } = "3. That security guards are operational;";
        public string Declaration4 { get; set; } = "4. That in view of the above, nothing shall prevent the payment of the amount due in accordance with the agreements.";
        public string ReplacedAndMountedParts { get; set; } = "REPLACED AND/OR MOUNTED PARTS";
        public string Code { get; set; } = "Code";
        public string Description { get; set; } = "Description";
        public string Qty { get; set; } = "Qty";
        public string NotesOrReserves { get; set; } = "Notes and/or reserves";
        public string TickIfNoComments { get; set; } = "Tick if there are no comments.";
        public string AcceptedOn { get; set; } = "Accepted on:";
        public string Page { get; set; } = "Page";
        public string Of { get; set; } = "of";
    }

    public static class InterventionReportPdf
    {
        /// <summary>
        /// Creates the PDF into a byte array.
        /// </summary>
        public static byte[] Create(InterventionReportData data, InterventionReportLabels? labels = null)
        {
            labels ??= new InterventionReportLabels();
            
            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page, data);

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Element(c => TitleBlock(c, labels.MinuteOfIntervention));

                        col.Item().PaddingTop(8).Element(c => ClientBlock(c, data, labels));

                        col.Item().PaddingTop(10).Element(c => TechnicianAndPurposeBlock(c, data, labels));

                        col.Item().PaddingTop(10).Element(c => MachinesAndDescriptionBlock(c, data, labels));

                        col.Item().PaddingTop(10).Element(c => ServiceTimesBlock(c, data, labels));

                        col.Item().PaddingTop(10).Element(c => ActivitiesBlock(c, data, labels));

                        col.Item().PaddingTop(10).Element(c => CustomerDeclarationBlock(c, labels));
                    });

                    page.Footer().Element(c => FooterBlock(c, data, labels, showPageNumber: true));
                });

                container.Page(page =>
                {
                    ConfigurePage(page, data);

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Element(c => TitleBlock(c, labels.ReplacedAndMountedParts));

                        col.Item().PaddingTop(8).Element(c => ReplacedPartsTable(c, data, labels));

                        col.Item().PaddingTop(12).Element(c => NotesBlock(c, data, labels));

                        col.Item().PaddingTop(12).Element(c => AcceptanceBlock(c, data, labels));
                    });

                    page.Footer().Element(c => FooterBlock(c, data, labels, showPageNumber: true));
                });
            });

            return doc.GeneratePdf();
        }

        /// <summary>
        /// Convenience: writes PDF to file.
        /// </summary>
        public static void CreateToFile(string filePath, InterventionReportData data, InterventionReportLabels? labels = null)
            => File.WriteAllBytes(filePath, Create(data, labels));

        private static void ConfigurePage(PageDescriptor page, InterventionReportData data)
        {
            page.Size(PageSizes.A4);
            page.Margin(35);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Element(c => HeaderBlock(c, data));
        }

        private static void HeaderBlock(IContainer container, InterventionReportData data)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(data.ProviderCompanyName).SemiBold().FontSize(12);
                        col.Item().Text(data.ProviderAddressLine);
                        col.Item().Text(data.ProviderContactsLine);
                        col.Item().Text(data.ProviderEmailWebLine);
                        col.Item().Text(data.ProviderLegalLine).FontColor(Colors.Grey.Darken1).FontSize(9);
                    });

                    // If you have a logo image: row.ConstantItem(120).Height(50).Image(logoBytes);
                    row.ConstantItem(1).Width(0); // placeholder
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
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

        private static void ClientBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
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

                    Row(t, labels.CompanyName, d.ClientCompanyName, labels.VatId, d.ClientVatId);
                    Row(t, labels.Address, d.ClientAddress, labels.Zip, d.ClientZip);
                    Row(t, labels.City, d.ClientCity, labels.Country, d.ClientCountry);
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

        private static void TechnicianAndPurposeBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text($"{labels.OurTechnician}{d.TechnicianName}").SemiBold();
                
                col.Item().PaddingTop(2).Text($"{labels.HasIntervenedAt}{d.PremisesDescription}{labels.For}");

                col.Item().PaddingTop(8).Column(c =>
                {
                    if (d.InterventionTypes != null && d.InterventionTypes.Count > 0)
                    {
                        foreach (var interventionType in d.InterventionTypes)
                        {
                            var translatedName = GetInterventionTypeTranslation(interventionType, labels.CultureCode);
                            CheckboxLine(c, true, translatedName);
                        }
                    }
                });
            });
        }

        private static void MachinesAndDescriptionBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.MachinesDevices).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).Text(string.IsNullOrWhiteSpace(d.MachinesDevices) ? " " : d.MachinesDevices);

                col.Item().PaddingTop(10).Text(labels.DescriptionOfIntervention).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(80)
                   .Text(string.IsNullOrWhiteSpace(d.InterventionDescription) ? " " : d.InterventionDescription);
            });
        }

        private static void ServiceTimesBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
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
                t.Cell().Element(ValueCell).Text(d.ServiceBegan.ToString("MM/dd/yyyy h:mm tt"));
                t.Cell().Element(LabelCell).Text(labels.ServiceEnded);
                t.Cell().Element(ValueCell).Text(d.ServiceEnded.ToString("MM/dd/yyyy h:mm tt"));
            });
        }

        private static void ActivitiesBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.Activities).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(70)
                    .Text(string.IsNullOrWhiteSpace(d.Activities) ? " " : d.Activities);
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

        private static void ReplacedPartsTable(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(110); // Code
                    cols.RelativeColumn();    // Description
                    cols.ConstantColumn(60);  // Qty
                });

                t.Header(h =>
                {
                    h.Cell().Element(HeaderCell).Text(labels.Code);
                    h.Cell().Element(HeaderCell).Text(labels.Description);
                    h.Cell().Element(HeaderCell).AlignRight().Text(labels.Qty);
                });

                if (d.ReplacedParts == null || d.ReplacedParts.Count == 0)
                {
                    t.Cell().ColumnSpan(3).Element(EmptyRowCell).Text(" ");
                    return;
                }

                foreach (var p in d.ReplacedParts)
                {
                    t.Cell().Element(ValueCell).Text(p.Code);
                    t.Cell().Element(ValueCell).Text(p.Description);
                    t.Cell().Element(ValueCell).AlignRight().Text(p.Qty.ToString());
                }
            });
        }

        private static void NotesBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
            {
                col.Item().Text(labels.NotesOrReserves).SemiBold();
                col.Item().PaddingTop(4).Element(ValueBox).MinHeight(90)
                    .Text(string.IsNullOrWhiteSpace(d.NotesOrReserves) ? " " : d.NotesOrReserves);

                col.Item().PaddingTop(6).DefaultTextStyle(x => x.FontColor(Colors.Grey.Darken1)).Row(row =>
                {
                    row.ConstantItem(12).Text("☐");
                    row.RelativeItem().Text(labels.TickIfNoComments);
                });
            });
        }

        private static void AcceptanceBlock(IContainer container, InterventionReportData d, InterventionReportLabels labels)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(labels.AcceptedOn).SemiBold();
                    col.Item().Text(d.AcceptedOn.ToString("MM/dd/yyyy"));
                });

                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(d.StampAndSignatureLabel).SemiBold();
                    col.Item().PaddingTop(18).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                });
            });
        }

        private static void FooterBlock(IContainer container, InterventionReportData data, InterventionReportLabels labels, bool showPageNumber)
        {
            container.Column(col =>
            {
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                col.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(data.ProviderAddressLine).FontSize(9);
                        c.Item().Text(data.ProviderContactsLine).FontSize(9);
                        c.Item().Text(data.ProviderEmailWebLine).FontSize(9);
                    });

                    if (showPageNumber)
                    {
                        row.ConstantItem(120).AlignRight().DefaultTextStyle(x => x.FontSize(9)).Text(t =>
                        {
                            t.Span($"{labels.Page} ");
                            t.CurrentPageNumber();
                            t.Span($" {labels.Of} ");
                            t.TotalPages();
                        });
                    }
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

        private static IContainer HeaderCell(IContainer c) =>
            c.PaddingVertical(4).PaddingHorizontal(4)
             .Background(Colors.Grey.Lighten4)
             .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
             .DefaultTextStyle(x => x.SemiBold());

      

        private static IContainer ValueCell(IContainer c) =>
            c.PaddingVertical(2).AlignMiddle();

       

        private static IContainer EmptyRowCell(IContainer c) =>
            c.PaddingVertical(18).Border(1).BorderColor(Colors.Grey.Lighten3);

        private static IContainer ValueBox(IContainer c) =>
            c.Border(1).BorderColor(Colors.Grey.Lighten3).Padding(6);

        /// <summary>
        /// Recupera la traduzione del tipo di intervento in base al CultureCode
        /// </summary>
        private static string GetInterventionTypeTranslation(CRM.Shared.InterventionType interventionType, string? cultureCode)
        {
            // Se non ci sono traduzioni, usa il nome di default
            if (interventionType.InterventionTypeLanguages == null || !interventionType.InterventionTypeLanguages.Any())
            {
                return interventionType.Name ?? "N/A";
            }

            // Se non è specificato un CultureCode, usa il nome di default
            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                return interventionType.Name ?? "N/A";
            }

            // Estrai il codice lingua dal CultureCode (es. "it-IT" -> "it")
            var languageCode = cultureCode.Split('-')[0];

            // Cerca la traduzione nella lingua richiesta
            var translation = interventionType.InterventionTypeLanguages
                .FirstOrDefault(x => x.Language != null && 
                                    (x.Language.LanguageCode == languageCode || 
                                     x.Language.LanguageCode == cultureCode));

            if (translation != null && !string.IsNullOrWhiteSpace(translation.Name))
            {
                return translation.Name;
            }

            // Fallback: prende la prima traduzione disponibile ordinata per Index
            var firstTranslation = interventionType.InterventionTypeLanguages
                .Where(x => x.Language != null && !string.IsNullOrWhiteSpace(x.Name))
                .OrderBy(x => x.Language?.Index ?? 99)
                .FirstOrDefault();

            return firstTranslation?.Name ?? interventionType.Name ?? "N/A";
        }
    }

}

