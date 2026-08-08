using CRM.Server.Services.Usage;

namespace CRM.Tests;

/// <summary>
/// Conversione da consumo a importo. E' la parte del registro che puo' sbagliare in silenzio:
/// un totale sbagliato di un fattore mille resta un numero credibile, e nessuno se ne accorge
/// finche' non arriva la fattura.
/// </summary>
public class AiCostTests
{
    /// <summary>Opus 4.8: 5 $ / 25 $ per milione di token.</summary>
    private static ModelPrice Opus => new() { Input = 5m, Output = 25m };

    private static AiPricingOptions Listino()
    {
        var options = new AiPricingOptions();
        options.Models["claude-opus-4-8"] = Opus;
        options.Models["claude-haiku-4-5"] = new ModelPrice { Input = 1m, Output = 5m };
        options.Operations["prebuilt-receipt"] = 0.01m;
        return options;
    }

    [Fact]
    public void I_prezzi_sono_per_milione_di_token()
    {
        // Un milione in ingresso e un milione in uscita = il prezzo di listino, esatto.
        var cost = AiCostCalculator.TokenCost(Opus, 1_000_000, 1_000_000, 0, 0);

        Assert.Equal(30m, cost);
    }

    [Fact]
    public void Una_chiamata_normale_costa_pochi_centesimi()
    {
        // Ordine di grandezza di uno smistamento ticket: ~3.000 token dentro, ~200 fuori.
        var cost = AiCostCalculator.TokenCost(Opus, 3_000, 200, 0, 0);

        Assert.Equal(0.02m, cost);
    }

    /// <summary>
    /// Il punto per cui la cache sta in colonne separate: allo stesso numero di token
    /// corrispondono importi diversi, e contarla come input normale gonfierebbe il totale.
    /// </summary>
    [Fact]
    public void La_cache_letta_costa_un_decimo_dell_input()
    {
        var input = AiCostCalculator.TokenCost(Opus, 100_000, 0, 0, 0);
        var cached = AiCostCalculator.TokenCost(Opus, 0, 0, 100_000, 0);

        Assert.Equal(input!.Value / 10m, cached!.Value);
    }

    [Fact]
    public void La_cache_scritta_costa_piu_dell_input()
    {
        var input = AiCostCalculator.TokenCost(Opus, 100_000, 0, 0, 0);
        var written = AiCostCalculator.TokenCost(Opus, 0, 0, 0, 100_000);

        Assert.True(written > input);
    }

    /// <summary>
    /// Un prezzo esplicito nel listino vince sul rapporto standard: e' l'unico modo di seguire
    /// un fornitore che cambi le proporzioni senza dover ricompilare.
    /// </summary>
    [Fact]
    public void Il_prezzo_della_cache_dichiarato_vince_sul_rapporto_standard()
    {
        var price = new ModelPrice { Input = 5m, Output = 25m, CacheRead = 2m };

        var cost = AiCostCalculator.TokenCost(price, 0, 0, 1_000_000, 0);

        Assert.Equal(2m, cost);
    }

    /// <summary>
    /// Un modello fuori listino non si stima. Uno zero si sommerebbe agli altri sparendo dal
    /// totale; un nullo resta visibile e si va a mettere il prezzo.
    /// </summary>
    [Fact]
    public void Un_modello_fuori_listino_non_viene_stimato()
    {
        var listino = Listino();

        Assert.Null(listino.FindModel("modello-che-non-esiste"));
        Assert.Null(AiCostCalculator.TokenCost(listino.FindModel("modello-che-non-esiste"), 1_000, 1_000, 0, 0));
    }

    /// <summary>
    /// Gli id datati dei modelli non devono costringere ad aggiornare il listino a ogni
    /// istantanea: la voce senza data copre le sue versioni.
    /// </summary>
    [Fact]
    public void Un_id_datato_ricade_sulla_voce_del_modello()
    {
        var price = Listino().FindModel("claude-haiku-4-5-20251001");

        Assert.NotNull(price);
        Assert.Equal(1m, price!.Input);
    }

    [Fact]
    public void Il_listino_ignora_maiuscole_e_minuscole()
    {
        Assert.NotNull(Listino().FindModel("Claude-Opus-4-8"));
    }

    /// <summary>
    /// Fra due voci che combaciano entrambe vince la piu' specifica, o un modello nuovo
    /// erediterebbe il prezzo di un suo omonimo piu' corto e piu' vecchio.
    /// </summary>
    [Fact]
    public void Tra_due_prefissi_vince_il_piu_lungo()
    {
        var options = new AiPricingOptions();
        options.Models["claude-opus"] = new ModelPrice { Input = 99m, Output = 99m };
        options.Models["claude-opus-4-8"] = Opus;

        Assert.Equal(5m, options.FindModel("claude-opus-4-8-20260101")!.Input);
    }

    [Fact]
    public void I_servizi_a_unita_si_moltiplicano_per_le_pagine()
    {
        var listino = Listino();

        Assert.Equal(0.05m, AiCostCalculator.UnitCost(listino.FindOperation("prebuilt-receipt"), 5));
    }

    [Fact]
    public void Un_operazione_fuori_listino_non_viene_stimata()
    {
        Assert.Null(AiCostCalculator.UnitCost(Listino().FindOperation("prebuilt-businessCard"), 3));
    }

    /// <summary>
    /// Somma dei consumi di un giro dell'assistente: le iterazioni si accumulano prima di
    /// diventare righe, e l'operatore deve restare associativo o i totali ballano.
    /// </summary>
    [Fact]
    public void I_consumi_di_piu_iterazioni_si_sommano()
    {
        var first = new TokenUsage(1000, 200, 50, 10);
        var second = new TokenUsage(1500, 300, 0, 0);

        var total = first + second;

        Assert.Equal(new TokenUsage(2500, 500, 50, 10), total);
        Assert.False(total.IsEmpty);
        Assert.True(default(TokenUsage).IsEmpty);
    }
}
