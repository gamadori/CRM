using CRM.Shared.DTOs;

namespace CRM.Tests;

/// <summary>
/// La matematica di preventivi, ordini e fatture. E' una sola: <see cref="QuoteMath"/>, usata
/// identica dai tre servizi - se un giorno qualcuno la duplica "solo per le fatture", questi test
/// restano verdi e il problema si vede altrove, quindi li accompagna
/// <see cref="I_tre_documenti_calcolano_la_riga_allo_stesso_modo"/>.
/// <para>
/// Quello che si difende qui non e' l'aritmetica in se', e' la <b>scelta</b> di arrotondare riga
/// per riga a due decimali. Cambiarla in "arrotonda alla fine" sposta i totali di qualche
/// centesimo su documenti gia' mandati al cliente e gia' spediti allo SdI: sono i centesimi che
/// non tornano quando il commercialista confronta imponibile e IVA.
/// </para>
/// </summary>
public class MoneyMathTests
{
    // ─── Riga singola ────────────────────────────────────────────────────────

    [Fact]
    public void Una_riga_senza_sconto_e_imponibile_piu_iva()
    {
        var (net, vat, total) = QuoteMath.Line(qty: 2, unit: 50m, discPct: 0m, vatRate: 22m);

        Assert.Equal(100m, net);
        Assert.Equal(22m, vat);
        Assert.Equal(122m, total);
    }

    [Fact]
    public void Lo_sconto_percentuale_toglie_dall_imponibile_non_dall_iva()
    {
        var (net, vat, total) = QuoteMath.Line(qty: 1, unit: 100m, discPct: 10m, vatRate: 22m);

        Assert.Equal(90m, net);
        Assert.Equal(19.8m, vat);   // il 22% del netto scontato, non del lordo
        Assert.Equal(109.8m, total);
        Assert.Equal(10m, QuoteMath.DiscountAmount(1, 100m, 10m));
    }

    [Fact]
    public void Uno_sconto_del_cento_per_cento_azzera_riga_e_iva()
    {
        var (net, vat, total) = QuoteMath.Line(qty: 3, unit: 80m, discPct: 100m, vatRate: 22m);

        Assert.Equal(0m, net);
        Assert.Equal(0m, vat);
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Una_riga_in_negativo_resta_negativa()
    {
        // Le note di credito e gli storni passano di qui: se l'arrotondamento tirasse verso lo
        // zero invece che lontano da zero, uno storno varrebbe un centesimo meno del suo addebito.
        var (net, vat, _) = QuoteMath.Line(qty: -1, unit: 2.345m, discPct: 0m, vatRate: 22m);

        Assert.Equal(-2.35m, net);
        Assert.Equal(-0.52m, vat);
    }

    [Theory]
    [InlineData("2.345", "2.35")]   // mezzo centesimo: si allontana da zero, non "al pari"
    [InlineData("2.344", "2.34")]
    [InlineData("2.355", "2.36")]
    public void Il_mezzo_centesimo_si_arrotonda_allontanandosi_da_zero(string unit, string atteso)
    {
        // I valori passano come stringa e non come double: 2.345 in virgola mobile non e'
        // esattamente 2.345, e il test finirebbe per misurare la conversione invece della regola.
        var prezzo = decimal.Parse(unit, System.Globalization.CultureInfo.InvariantCulture);

        var (net, _, _) = QuoteMath.Line(qty: 1, unit: prezzo, discPct: 0m, vatRate: 0m);

        Assert.Equal(decimal.Parse(atteso, System.Globalization.CultureInfo.InvariantCulture), net);
    }

    // ─── Somma di piu' righe ─────────────────────────────────────────────────

    [Fact]
    public void Il_totale_somma_righe_gia_arrotondate_non_arrotonda_la_somma()
    {
        // Tre righe da 0.125: ognuna diventa 0.13, quindi 0.39. Arrotondando invece la somma
        // (0.375) verrebbe 0.38, e il documento non tornerebbe con le sue stesse righe.
        var righe = Enumerable.Range(0, 3)
            .Select(_ => QuoteMath.Line(qty: 1, unit: 0.125m, discPct: 0m, vatRate: 0m))
            .ToList();

        Assert.All(righe, r => Assert.Equal(0.13m, r.net));
        Assert.Equal(0.39m, righe.Sum(r => r.net));
    }

    [Fact]
    public void Imponibile_piu_iva_fa_sempre_il_totale_riga_per_riga()
    {
        var righe = new[]
        {
            QuoteMath.Line(3, 19.99m, 5m, 22m),
            QuoteMath.Line(1, 0.01m, 0m, 4m),
            QuoteMath.Line(7, 123.45m, 33.33m, 10m)
        };

        foreach (var (net, vat, total) in righe)
            Assert.Equal(net + vat, total);

        var imponibile = righe.Sum(r => r.net);
        var iva = righe.Sum(r => r.vat);

        Assert.Equal(imponibile + iva, righe.Sum(r => r.total));
    }

    [Fact]
    public void I_tre_documenti_arrivano_agli_stessi_totali()
    {
        // Preventivo -> ordine -> fattura e' una catena: le stesse righe devono valere gli stessi
        // soldi in tutti e tre, altrimenti il cliente firma una cifra e ne fattura un'altra.
        // Si invocano i tre Recalculate privati sulle rispettive entita', con righe identiche.
        var preventivo = new CRM.Shared.Quote
        {
            Rows = Righe((q, u, s, i) => new CRM.Shared.QuoteRow
            { Quantity = q, UnitPrice = u, DiscountPct = s, VatRate = i }).ToList()
        };

        var ordine = new CRM.Shared.Order
        {
            Rows = Righe((q, u, s, i) => new CRM.Shared.OrderRow
            { Quantity = q, UnitPrice = u, DiscountPct = s, VatRate = i }).ToList()
        };

        var fattura = new CRM.Shared.Invoice
        {
            Rows = Righe((q, u, s, i) => new CRM.Shared.InvoiceRow
            { Quantity = q, UnitPrice = u, DiscountPct = s, VatRate = i }).ToList()
        };

        Ricalcola(typeof(CRM.Server.Services.QuotesService), preventivo);
        Ricalcola(typeof(CRM.Server.Services.OrdersService), ordine);
        Ricalcola(typeof(CRM.Server.Services.InvoicesService), fattura);

        Assert.Equal(preventivo.Subtotal, ordine.Subtotal);
        Assert.Equal(preventivo.Subtotal, fattura.Subtotal);

        Assert.Equal(preventivo.TotalVat, ordine.TotalVat);
        Assert.Equal(preventivo.TotalVat, fattura.TotalVat);

        Assert.Equal(preventivo.Total, ordine.Total);
        Assert.Equal(preventivo.Total, fattura.Total);

        // E il totale non e' zero per caso: se lo fosse, il confronto sopra non direbbe niente.
        Assert.True(preventivo.Total > 0);
    }

    /// <summary>Le stesse tre righe, costruite nel tipo di riga di ciascun documento.</summary>
    private static IEnumerable<T> Righe<T>(Func<decimal, decimal, decimal, decimal, T> riga)
    {
        yield return riga(3m, 19.99m, 5m, 22m);
        yield return riga(1m, 0.01m, 0m, 4m);
        yield return riga(7m, 123.45m, 33.33m, 10m);
    }

    private static void Ricalcola(Type servizio, object documento)
    {
        var ricalcolo = servizio.GetMethod("Recalculate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(ricalcolo);
        ricalcolo!.Invoke(null, new[] { documento });
    }
}
