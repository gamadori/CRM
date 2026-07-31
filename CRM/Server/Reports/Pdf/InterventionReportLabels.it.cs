namespace CRM.Server.Reports.Pdf
{
    /// <summary>
    /// Traduzioni italiane per il report di intervento
    /// </summary>
    public static class InterventionReportLabelsIT
    {
        public static InterventionReportLabels GetItalianLabels()
        {
            return new InterventionReportLabels
            {
                CultureCode = "it-IT",
                MinuteOfIntervention = "VERBALE DI INTERVENTO",
                Client = "Cliente",
                CompanyName = "Ragione sociale:",
                Address = "Indirizzo:",
                Zip = "CAP:",
                City = "Città:",
                Country = "Paese:",
                VatId = "P.IVA:",
                OurTechnician = "Il nostro Tecnico, Sig. ",
                HasIntervenedAt = "è intervenuto presso ",
                For = " per:",
                ToInstall = "Installare",
                ToTest = "Testare",
                ToCheck = "Verificare",
                ToRepair = "Riparare",
                ToSuggestRecommend = "Suggerire / Raccomandare",
                DevelopmentAndDesign = "Sviluppo e Progettazione",
                MachinesDevices = "Macchine / Dispositivi",
                DescriptionOfIntervention = "Descrizione dell'Intervento",
                ServiceBegan = "Inizio servizio:",
                ServiceEnded = "Fine servizio:",
                Activities = "Attività",
                CustomerDeclaration = "Al termine dell'intervento, il cliente dichiara:",
                Declaration1 = "1. Che le funzioni delle macchine/dispositivi sono tutte funzionanti;",
                Declaration2 = "2. Che il funzionamento delle stesse è regolare;",
                Declaration3 = "3. Che i dispositivi di sicurezza sono operativi;",
                Declaration4 = "4. Che in considerazione di quanto sopra, nulla osta al pagamento dell'importo dovuto secondo gli accordi.",
                ReplacedAndMountedParts = "PARTI SOSTITUITE E/O MONTATE",
                Code = "Codice",
                Description = "Descrizione",
                Qty = "Qtà",
                NotesOrReserves = "Note e/o riserve",
                TickIfNoComments = "Barrare se non ci sono commenti.",
                AcceptedOn = "Accettato in data:",
                Page = "Pagina",
                Of = "di"
            };
        }
    }
}
