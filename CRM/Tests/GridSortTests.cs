using CRM.Server.Services;

namespace CRM.Tests;

/// <summary>
/// Ordinamento degli elenchi documentali. Le griglie ordinano per i nomi dei DTO, i servizi
/// interrogano le entita': i criteri vanno tradotti, e quelli intraducibili scartati invece di far
/// esplodere la query (l'eccezione veniva inghiottita e l'elenco spariva senza dare errore).
/// </summary>
public class GridSortTests
{
    // ─── Ordini ──────────────────────────────────────────────────────────────

    [Fact]
    public void Ordini_le_colonne_dell_entita_passano_invariate()
    {
        Assert.Equal("Number asc", OrdersService.TranslateOrderBy("Number asc"));
        Assert.Equal("Date desc", OrdersService.TranslateOrderBy("Date desc"));
        Assert.Equal("Total asc", OrdersService.TranslateOrderBy("Total asc"));
    }

    [Fact]
    public void Ordini_le_colonne_del_DTO_diventano_percorsi_dell_entita()
    {
        Assert.Equal("Company.RagioneSociale asc", OrdersService.TranslateOrderBy("CompanyName asc"));
        Assert.Equal("Quote.Number desc", OrdersService.TranslateOrderBy("QuoteNumber desc"));
        Assert.Equal("Deal.Name asc", OrdersService.TranslateOrderBy("DealName asc"));
    }

    [Fact]
    public void Ordini_il_verso_puo_mancare()
    {
        Assert.Equal("Company.RagioneSociale", OrdersService.TranslateOrderBy("CompanyName"));
    }

    [Fact]
    public void Ordini_piu_criteri_vengono_tradotti_uno_a_uno()
    {
        Assert.Equal(
            "Company.RagioneSociale asc, Date desc",
            OrdersService.TranslateOrderBy("CompanyName asc, Date desc"));
    }

    /// <summary>Il caso che svuotava l'elenco: colonna che l'entita' non ha.</summary>
    [Fact]
    public void Ordini_una_colonna_sconosciuta_viene_scartata()
    {
        Assert.Null(OrdersService.TranslateOrderBy("NonEsiste asc"));
        Assert.Equal("Date desc", OrdersService.TranslateOrderBy("NonEsiste asc, Date desc"));
    }

    [Fact]
    public void Ordini_nessun_ordinamento_richiesto_significa_nessun_criterio()
    {
        Assert.Null(OrdersService.TranslateOrderBy(null));
        Assert.Null(OrdersService.TranslateOrderBy("   "));
    }

    // ─── Preventivi ──────────────────────────────────────────────────────────

    [Fact]
    public void Preventivi_azienda_diventa_ragione_sociale()
    {
        Assert.Equal("Company.RagioneSociale asc", QuotesService.TranslateOrderBy("CompanyName asc"));
        Assert.Equal("Number asc", QuotesService.TranslateOrderBy("Number asc"));
        Assert.Equal("Total desc", QuotesService.TranslateOrderBy("Total desc"));
    }

    /// <summary>Il preventivo non ha una navigazione verso l'ordine: il criterio si scarta.</summary>
    [Fact]
    public void Preventivi_il_numero_ordine_non_e_ordinabile()
    {
        Assert.Null(QuotesService.TranslateOrderBy("OrderNumber asc"));
    }

    // ─── Fatture ─────────────────────────────────────────────────────────────

    [Fact]
    public void Fatture_azienda_e_ordine_diventano_percorsi()
    {
        Assert.Equal("Company.RagioneSociale asc", InvoicesService.TranslateOrderBy("CompanyName asc"));
        Assert.Equal("Order.Number desc", InvoicesService.TranslateOrderBy("OrderNumber desc"));
        Assert.Equal("Number asc", InvoicesService.TranslateOrderBy("Number asc"));
    }

    // ─── Commesse ────────────────────────────────────────────────────────────

    [Fact]
    public void Commesse_azienda_e_ordine_diventano_percorsi()
    {
        Assert.Equal("Company.RagioneSociale asc", CommesseService.TranslateOrderBy("CompanyName asc"));
        Assert.Equal("Code asc", CommesseService.TranslateOrderBy("Code asc"));
        Assert.Equal("EndDatePlanned desc", CommesseService.TranslateOrderBy("EndDatePlanned desc"));
    }

    /// <summary>La commessa e' agganciata alla riga d'ordine: il numero sta due livelli sotto.</summary>
    [Fact]
    public void Commesse_il_numero_ordine_passa_dalla_riga()
    {
        Assert.Equal("OrderRow.Order.Number asc", CommesseService.TranslateOrderBy("OrderNumber asc"));
    }

    /// <summary>ExpectedEndDate esiste solo sul DTO: la griglia non la ordina, ma se ci provasse
    /// il criterio verrebbe scartato invece di svuotare l'elenco.</summary>
    [Fact]
    public void Commesse_una_colonna_calcolata_del_DTO_viene_scartata()
    {
        Assert.Null(CommesseService.TranslateOrderBy("ExpectedEndDate asc"));
    }
}
