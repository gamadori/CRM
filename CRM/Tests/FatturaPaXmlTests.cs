using System.Globalization;
using System.Xml.Linq;
using CRM.Server.Services;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// XML della fattura elettronica. E' l'unico documento del CRM che viene giudicato da qualcun
/// altro: lo SdI lo scarta e la fattura non esiste, con i tempi di rifiuto e reinvio che ne
/// seguono. Un errore qui non lo vede nessuno finche' non torna indietro.
/// <para>
/// I test guardano le cose che si rompono davvero: la <b>cultura</b> con cui si scrivono gli
/// importi (su una macchina italiana "1234,56" con la virgola e' XML valido e fattura scartata),
/// il riepilogo per aliquota che deve quadrare con le righe, e i campi obbligatori che devono
/// esserci anche quando l'anagrafica del cliente e' incompleta - il momento in cui e' piu' facile
/// che manchino.
/// </para>
/// </summary>
public class FatturaPaXmlTests
{
    private static readonly XNamespace Ns = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    private static Company Emittente() => new()
    {
        RagioneSociale = "Redg Srl",
        PIva = "IT 01234567890",
        Indirizzo = "Via Roma 1",
        Cap = "40100",
        Citta = "Bologna",
        Provincia = "bo"
    };

    private static Company Cliente() => new()
    {
        RagioneSociale = "Cliente Spa",
        PIva = "09876543210",
        Indirizzo = "Corso Italia 9",
        Cap = "20100",
        Citta = "Milano",
        Provincia = "MI"
    };

    private static Invoice Fattura(params InvoiceRow[] righe)
    {
        var fattura = new Invoice
        {
            Id = 12,
            Number = "FT-2026-0012",
            Date = new DateTime(2026, 3, 4),
            Company = Cliente(),
            Rows = righe.ToList()
        };

        foreach (var r in fattura.Rows)
        {
            var (net, vat, total) = CRM.Shared.DTOs.QuoteMath.Line(r.Quantity, r.UnitPrice, r.DiscountPct, r.VatRate);
            r.LineNet = net;
            r.LineVat = vat;
            r.LineTotal = total;
        }

        fattura.Subtotal = fattura.Rows.Sum(r => r.LineNet);
        fattura.TotalVat = fattura.Rows.Sum(r => r.LineVat);
        fattura.Total = fattura.Subtotal + fattura.TotalVat;

        return fattura;
    }

    private static InvoiceRow Riga(decimal qty, decimal prezzo, decimal iva, decimal sconto = 0, string? natura = null)
        => new() { Description = "Servizio", Quantity = qty, UnitPrice = prezzo, VatRate = iva, DiscountPct = sconto, Natura = natura };

    private static XElement Xml(Invoice fattura)
        => XDocument.Parse(FatturaPaXmlBuilder.Build(fattura, Emittente(), null)).Root!;

    [Fact]
    public void Gli_importi_usano_il_punto_anche_su_una_macchina_italiana()
    {
        // Il caso che manda indietro le fatture: cultura di sistema italiana, decimali con la
        // virgola. Si forza la cultura del thread per riprodurlo davvero, invece di fidarsi che
        // la macchina di chi esegue i test sia configurata come quella di produzione.
        var precedente = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("it-IT");

        try
        {
            var xml = Xml(Fattura(Riga(qty: 1, prezzo: 1234.56m, iva: 22m)));

            var totale = xml.Descendants("ImportoTotaleDocumento").Single().Value;
            var prezzo = xml.Descendants("PrezzoUnitario").Single().Value;
            var quantita = xml.Descendants("Quantita").Single().Value;

            Assert.Equal("1506.16", totale);   // 1234.56 + 271.60
            Assert.Equal("1234.56", prezzo);
            Assert.Equal("1.00", quantita);

            Assert.DoesNotContain(",", totale);
            Assert.DoesNotContain(",", prezzo);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = precedente;
        }
    }

    [Fact]
    public void Il_riepilogo_per_aliquota_quadra_con_le_righe()
    {
        var fattura = Fattura(
            Riga(2, 100m, 22m),
            Riga(1, 50m, 22m),
            Riga(3, 10m, 10m));

        var xml = Xml(fattura);
        var riepiloghi = xml.Descendants("DatiRiepilogo").ToList();

        Assert.Equal(2, riepiloghi.Count);   // due aliquote, non tre righe

        decimal Somma(string tag) => riepiloghi
            .Sum(r => decimal.Parse(r.Element(tag)!.Value, CultureInfo.InvariantCulture));

        Assert.Equal(fattura.Subtotal, Somma("ImponibileImporto"));
        Assert.Equal(fattura.TotalVat, Somma("Imposta"));

        var totale = decimal.Parse(xml.Descendants("ImportoTotaleDocumento").Single().Value, CultureInfo.InvariantCulture);
        Assert.Equal(Somma("ImponibileImporto") + Somma("Imposta"), totale);
    }

    [Fact]
    public void Una_riga_esente_porta_la_natura_nel_riepilogo()
    {
        var xml = Xml(Fattura(Riga(1, 100m, iva: 0m, natura: "N2.2")));

        var riepilogo = xml.Descendants("DatiRiepilogo").Single();

        Assert.Equal("0.00", riepilogo.Element("AliquotaIVA")!.Value);
        Assert.Equal("N2.2", riepilogo.Element("Natura")!.Value);
    }

    [Fact]
    public void Senza_codice_destinatario_si_usa_il_segnaposto_e_la_pec()
    {
        var fattura = Fattura(Riga(1, 100m, 22m));
        fattura.PecDestinatario = "cliente@pec.it";

        var xml = Xml(fattura);

        Assert.Equal("0000000", xml.Descendants("CodiceDestinatario").Single().Value);
        Assert.Equal("cliente@pec.it", xml.Descendants("PECDestinatario").Single().Value);
    }

    [Fact]
    public void La_partita_iva_perde_prefisso_e_spazi()
    {
        var xml = Xml(Fattura(Riga(1, 100m, 22m)));

        var cedente = xml.Descendants("CedentePrestatore").Single();

        Assert.Equal("01234567890", cedente.Descendants("IdCodice").First().Value);
    }

    [Fact]
    public void Un_cliente_con_anagrafica_incompleta_non_produce_campi_vuoti()
    {
        // Sede e' obbligatoria: senza indirizzo lo SdI scarta. Meglio "N/D" e un CAP finto che un
        // elemento vuoto, perche' il primo si vede nel documento e il secondo torna indietro.
        var fattura = Fattura(Riga(1, 100m, 22m));
        fattura.Company = new Company { RagioneSociale = "Cliente Senza Indirizzo" };

        var xml = Xml(fattura);
        var sede = xml.Descendants("CessionarioCommittente").Single().Element("Sede")!;

        Assert.Equal("N/D", sede.Element("Indirizzo")!.Value);
        Assert.Equal("00000", sede.Element("CAP")!.Value);
        Assert.Equal("N/D", sede.Element("Comune")!.Value);
        Assert.Equal("IT", sede.Element("Nazione")!.Value);
        Assert.Null(sede.Element("Provincia"));   // meglio assente che vuota
    }

    [Fact]
    public void La_provincia_va_in_maiuscolo_di_due_lettere()
    {
        var xml = Xml(Fattura(Riga(1, 100m, 22m)));

        var sedeEmittente = xml.Descendants("CedentePrestatore").Single().Element("Sede")!;

        Assert.Equal("BO", sedeEmittente.Element("Provincia")!.Value);
    }

    [Fact]
    public void Le_righe_sono_numerate_in_ordine_a_partire_da_uno()
    {
        var fattura = Fattura(
            Riga(1, 10m, 22m),
            Riga(1, 20m, 22m),
            Riga(1, 30m, 22m));

        // Ordine di inserimento volutamente diverso da quello di stampa.
        var righe = fattura.Rows.ToList();
        righe[0].SortOrder = 2;
        righe[1].SortOrder = 0;
        righe[2].SortOrder = 1;

        var xml = Xml(fattura);
        var dettagli = xml.Descendants("DettaglioLinee").ToList();

        Assert.Equal(new[] { "1", "2", "3" }, dettagli.Select(d => d.Element("NumeroLinea")!.Value));
        Assert.Equal(
            new[] { "20.00", "30.00", "10.00" },
            dettagli.Select(d => d.Element("PrezzoUnitario")!.Value));
    }

    [Fact]
    public void Il_documento_dichiara_versione_e_namespace_attesi()
    {
        var xml = Xml(Fattura(Riga(1, 100m, 22m)));

        Assert.Equal(Ns + "FatturaElettronica", xml.Name);
        Assert.Equal("FPR12", xml.Attribute("versione")!.Value);
        Assert.Equal("FPR12", xml.Descendants("FormatoTrasmissione").Single().Value);
    }
}
