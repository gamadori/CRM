using System.Globalization;
using System.Xml.Linq;
using CRM.Shared;

namespace CRM.Server.Services
{
    /// <summary>
    /// Costruisce l'XML FatturaPA (tracciato ministeriale v1.2) a partire da una Fattura,
    /// dai dati dell'azienda emittente (cedente/prestatore) e dal regime fiscale.
    ///
    /// NOTA: questo generatore copre i blocchi principali del tracciato. Prima dell'uso in
    /// produzione va validato contro l'XSD ufficiale e completato con i blocchi opzionali
    /// eventualmente richiesti dal caso d'uso (DatiPagamento, bollo, ritenute, cassa, ecc.).
    /// La firma (CAdES/XAdES), la trasmissione a SdI e la conservazione sono a carico del provider.
    /// </summary>
    public static class FatturaPaXmlBuilder
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly XNamespace Ns = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

        public static string Build(Invoice invoice, Company issuer, string? regimeFiscale)
        {
            regimeFiscale = string.IsNullOrWhiteSpace(regimeFiscale) ? "RF01" : regimeFiscale;
            var client = invoice.Company;

            var header = new XElement("FatturaElettronicaHeader",
                BuildDatiTrasmissione(invoice, issuer),
                BuildCedentePrestatore(issuer, regimeFiscale),
                BuildCessionarioCommittente(client));

            var body = new XElement("FatturaElettronicaBody",
                BuildDatiGenerali(invoice),
                BuildDatiBeniServizi(invoice));

            var root = new XElement(Ns + "FatturaElettronica",
                new XAttribute("versione", "FPR12"),
                new XAttribute(XNamespace.Xmlns + "p", Ns.NamespaceName),
                header,
                body);

            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
            return doc.Declaration + "\r\n" + doc.ToString();
        }

        private static XElement BuildDatiTrasmissione(Invoice invoice, Company issuer)
        {
            var codice = string.IsNullOrWhiteSpace(invoice.CodiceDestinatario) ? "0000000" : invoice.CodiceDestinatario.Trim();

            var dt = new XElement("DatiTrasmissione",
                new XElement("IdTrasmittente",
                    new XElement("IdPaese", "IT"),
                    new XElement("IdCodice", DigitsOnly(issuer.PIva))),
                new XElement("ProgressivoInvio", (invoice.Id > 0 ? invoice.Id : 1).ToString(Inv)),
                new XElement("FormatoTrasmissione", "FPR12"),
                new XElement("CodiceDestinatario", codice));

            if (codice == "0000000" && !string.IsNullOrWhiteSpace(invoice.PecDestinatario))
                dt.Add(new XElement("PECDestinatario", invoice.PecDestinatario));

            return dt;
        }

        private static XElement BuildCedentePrestatore(Company issuer, string regimeFiscale)
        {
            return new XElement("CedentePrestatore",
                new XElement("DatiAnagrafici",
                    new XElement("IdFiscaleIVA",
                        new XElement("IdPaese", "IT"),
                        new XElement("IdCodice", DigitsOnly(issuer.PIva))),
                    string.IsNullOrWhiteSpace(issuer.CodiceFiscale) ? null : new XElement("CodiceFiscale", issuer.CodiceFiscale),
                    new XElement("Anagrafica",
                        new XElement("Denominazione", issuer.RagioneSociale ?? string.Empty)),
                    new XElement("RegimeFiscale", regimeFiscale)),
                BuildSede(issuer));
        }

        private static XElement BuildCessionarioCommittente(Company? client)
        {
            var anagrafici = new XElement("DatiAnagrafici");

            if (!string.IsNullOrWhiteSpace(client?.PIva))
            {
                anagrafici.Add(new XElement("IdFiscaleIVA",
                    new XElement("IdPaese", "IT"),
                    new XElement("IdCodice", DigitsOnly(client!.PIva))));
            }
            if (!string.IsNullOrWhiteSpace(client?.CodiceFiscale))
                anagrafici.Add(new XElement("CodiceFiscale", client!.CodiceFiscale));

            anagrafici.Add(new XElement("Anagrafica",
                new XElement("Denominazione", client?.RagioneSociale ?? string.Empty)));

            return new XElement("CessionarioCommittente",
                anagrafici,
                BuildSede(client));
        }

        private static XElement BuildSede(Company? c)
        {
            return new XElement("Sede",
                new XElement("Indirizzo", string.IsNullOrWhiteSpace(c?.Indirizzo) ? "N/D" : c!.Indirizzo),
                new XElement("CAP", NormalizeCap(c?.Cap)),
                new XElement("Comune", string.IsNullOrWhiteSpace(c?.Citta) ? "N/D" : c!.Citta),
                string.IsNullOrWhiteSpace(c?.Provincia) ? null : new XElement("Provincia", c!.Provincia!.Trim().ToUpperInvariant()),
                new XElement("Nazione", "IT"));
        }

        private static XElement BuildDatiGenerali(Invoice invoice)
        {
            var doc = new XElement("DatiGeneraliDocumento",
                new XElement("TipoDocumento", string.IsNullOrWhiteSpace(invoice.TipoDocumento) ? "TD01" : invoice.TipoDocumento),
                new XElement("Divisa", "EUR"),
                new XElement("Data", invoice.Date.ToString("yyyy-MM-dd", Inv)),
                new XElement("Numero", invoice.Number ?? invoice.Id.ToString(Inv)),
                new XElement("ImportoTotaleDocumento", Money(invoice.Total)));

            if (!string.IsNullOrWhiteSpace(invoice.Causale))
                doc.Add(new XElement("Causale", invoice.Causale));

            return new XElement("DatiGenerali", doc);
        }

        private static XElement BuildDatiBeniServizi(Invoice invoice)
        {
            var dati = new XElement("DatiBeniServizi");

            int line = 1;
            foreach (var r in invoice.Rows.OrderBy(x => x.SortOrder))
            {
                var dettaglio = new XElement("DettaglioLinee",
                    new XElement("NumeroLinea", line.ToString(Inv)),
                    new XElement("Descrizione", string.IsNullOrWhiteSpace(r.Description) ? "-" : r.Description),
                    new XElement("Quantita", r.Quantity.ToString("0.00", Inv)),
                    new XElement("PrezzoUnitario", Money(r.UnitPrice)));

                if (r.DiscountPct != 0)
                {
                    dettaglio.Add(new XElement("ScontoMaggiorazione",
                        new XElement("Tipo", "SC"),
                        new XElement("Percentuale", r.DiscountPct.ToString("0.00", Inv))));
                }

                dettaglio.Add(new XElement("PrezzoTotale", Money(r.LineNet)));
                dettaglio.Add(new XElement("AliquotaIVA", r.VatRate.ToString("0.00", Inv)));

                if (r.VatRate == 0 && !string.IsNullOrWhiteSpace(r.Natura))
                    dettaglio.Add(new XElement("Natura", r.Natura));

                dati.Add(dettaglio);
                line++;
            }

            // Riepilogo per aliquota (raggruppa imponibile/imposta per VatRate + Natura)
            var groups = invoice.Rows
                .GroupBy(r => new { r.VatRate, Natura = r.VatRate == 0 ? (r.Natura ?? string.Empty) : string.Empty });

            foreach (var g in groups)
            {
                var riepilogo = new XElement("DatiRiepilogo",
                    new XElement("AliquotaIVA", g.Key.VatRate.ToString("0.00", Inv)));

                if (g.Key.VatRate == 0 && !string.IsNullOrWhiteSpace(g.Key.Natura))
                    riepilogo.Add(new XElement("Natura", g.Key.Natura));

                riepilogo.Add(new XElement("ImponibileImporto", Money(g.Sum(x => x.LineNet))));
                riepilogo.Add(new XElement("Imposta", Money(g.Sum(x => x.LineVat))));
                riepilogo.Add(new XElement("EsigibilitaIVA", "I"));

                dati.Add(riepilogo);
            }

            return dati;
        }

        private static string Money(decimal value) => value.ToString("0.00", Inv);

        private static string DigitsOnly(string? value)
            => string.IsNullOrWhiteSpace(value) ? "00000000000" : new string(value.Where(char.IsDigit).ToArray());

        private static string NormalizeCap(string? cap)
        {
            var digits = new string((cap ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 5 ? digits : "00000";
        }
    }
}
