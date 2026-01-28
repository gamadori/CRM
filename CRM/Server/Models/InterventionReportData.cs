using System;
using System.Collections.Generic;
using System.IO;
using CRM.Shared;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRM.Server.Models
{
    // NuGet: QuestPDF (latest)
    // using QuestPDF.Fluent;
    // using QuestPDF.Helpers;
    // using QuestPDF.Infrastructure;



    public class InterventionReportData
    {
        // Company / header (your company)
        public string ProviderCompanyName { get; set; } = "A-Plus Automation s.r.l.";
        public string ProviderAddressLine { get; set; } = "VIA SELVA 23/25 - IT";
        public string ProviderContactsLine { get; set; } = "Tel. +39 0543 481142 / Fax +39 0543 480770";
        public string ProviderEmailWebLine { get; set; } = "info@a-plusautomation.com  |  www.a-plusautomation.com";
        public string ProviderLegalLine { get; set; } = "VAT IT 04349600405 – REA: FO-404194";

        // Client
        public string ClientCompanyName { get; set; } = "";
        public string ClientAddress { get; set; } = "";
        public string ClientZip { get; set; } = "";
        public string ClientCity { get; set; } = "";
        public string ClientCountry { get; set; } = "";
        public string ClientVatId { get; set; } = "";

        // Technician & intervention
        public string TechnicianName { get; set; } = "";
        public string PremisesDescription { get; set; } = "your premises";


        public List<InterventionType> InterventionTypes { get; set; } = new();

        public string MachinesDevices { get; set; } = "";
        public string InterventionDescription { get; set; } = "";
        public DateTime ServiceBegan { get; set; } = DateTime.Now;
        public DateTime ServiceEnded { get; set; } = DateTime.Now;

        public string Activities { get; set; } = "";

        // Page 2
        public List<ReplacedPartRow> ReplacedParts { get; set; } = new();
        public string NotesOrReserves { get; set; } = "";
        public DateTime AcceptedOn { get; set; } = DateTime.Now;
        public string StampAndSignatureLabel { get; set; } = "Stamp & Signature";

        public class ReplacedPartRow
        {
            public string Code { get; set; } = "";
            public string Description { get; set; } = "";
            public int Qty { get; set; }
        }
    }
}
