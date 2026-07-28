using CRM.Client.Services;
using CRM.Server.Extensions;
using CRM.Server.Services;
using CRM.Shared;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Spostamento del piano su una nuova consegna (RescheduleAsync). La regola che questi test
/// difendono: in avanti le fasi già avviate restano alle loro date, indietro si spostano anche
/// loro, altrimenti le fasi ancora da fare verrebbero trascinate sopra a quelle in corso.
/// </summary>
public class RescheduleTests
{
    private static CommesseService Servizio(ProductionTestContext ctx)
        => new(ctx.Db, ctx.Permits, Substitute.For<ILogEventService>());

    /// <summary>
    /// Scenario comune: consegna a +30 giorni, una fase ancora da fare e una già avviata.
    /// Le date di partenza sono giorni lavorativi, così lo scarto atteso è esatto.
    /// </summary>
    private static (ProductionTestContext ctx, DateTime consegna) Scenario()
    {
        var ctx = ProductionTestContext.ComeAdmin();
        var commessa = ctx.CreaCommessa();
        var consegna = DateTime.Today.AddDays(30).PreviousWorkday();
        commessa.EndDatePlanned = consegna;
        commessa.StartDatePlanned = DateTime.Today.NextWorkday();
        ctx.Db.SaveChanges();

        ctx.CreaFase(1, nome: "Da fare", stato: CommessaFaseStates.Pending);
        ctx.CreaFase(2, nome: "In corso", stato: CommessaFaseStates.InProgress);
        return (ctx, consegna);
    }

    [Fact]
    public async Task Posticipando_la_consegna_le_fasi_da_fare_si_spostano()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;
        var prima = ctx.Rileggi(1);

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna.AddWorkdays(10));

        Assert.True(resp.State);
        var dopo = ctx.Rileggi(1);
        Assert.Equal(prima.StartDate.AddWorkdays(10), dopo.StartDate);
        Assert.Equal(prima.EndDate.AddWorkdays(10), dopo.EndDate);
    }

    [Fact]
    public async Task Posticipando_la_consegna_le_fasi_avviate_restano_ferme()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;
        var prima = ctx.Rileggi(2);

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna.AddWorkdays(10));

        Assert.True(resp.State);
        var dopo = ctx.Rileggi(2);
        Assert.Equal(prima.StartDate, dopo.StartDate);
        Assert.Equal(prima.EndDate, dopo.EndDate);
    }

    [Fact]
    public async Task Anticipando_la_consegna_si_spostano_anche_le_fasi_avviate()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;
        var prima = ctx.Rileggi(2);

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna.SubtractWorkdays(5));

        Assert.True(resp.State);
        var dopo = ctx.Rileggi(2);
        Assert.Equal(prima.StartDate.SubtractWorkdays(5), dopo.StartDate);
        Assert.Equal(prima.EndDate.SubtractWorkdays(5), dopo.EndDate);
    }

    [Fact]
    public async Task La_nuova_consegna_finisce_sulla_commessa()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;
        var nuova = consegna.AddWorkdays(10);

        var resp = await Servizio(ctx).RescheduleAsync(1, nuova);

        Assert.True(resp.State);
        var commessa = ctx.RileggiCommessa();
        Assert.Equal(nuova, commessa.EndDatePlanned);
        // l'inizio segue la prima fase, non la traslazione teorica
        Assert.Equal(ctx.Db.CommessaFasi.Min(f => f.StartDate).Date, commessa.StartDatePlanned.Date);
    }

    /// <summary>
    /// Una consegna nel weekend arrotonda al venerdì: lo scarto va calcolato sulla data
    /// normalizzata, o le fasi si spostano di un giorno più del dovuto.
    /// </summary>
    [Fact]
    public async Task Una_consegna_nel_weekend_arrotonda_al_giorno_lavorativo_precedente()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;

        var sabato = consegna.AddWorkdays(10);
        while (sabato.DayOfWeek != DayOfWeek.Saturday)
            sabato = sabato.AddDays(1);

        var resp = await Servizio(ctx).RescheduleAsync(1, sabato);

        Assert.True(resp.State);
        Assert.Equal(sabato.PreviousWorkday(), ctx.RileggiCommessa().EndDatePlanned);
    }

    [Fact]
    public async Task Spostare_sulla_stessa_data_viene_rifiutato()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna);

        Assert.False(resp.State);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.Code);
    }

    [Fact]
    public async Task Una_commessa_consegnata_non_si_riprogramma()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;
        var commessa = ctx.Db.Commesse.Single(c => c.Id == 1);
        commessa.State = CommessaStates.Delivered;
        ctx.Db.SaveChanges();

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna.AddWorkdays(10));

        Assert.False(resp.State);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.Code);
    }

    /// <summary>
    /// Il buco che l'operazione chiude: salvare la commessa con una consegna diversa spostava
    /// l'intestazione e lasciava le fasi dov'erano, senza segnalare nulla.
    /// </summary>
    [Fact]
    public async Task Salvare_la_commessa_non_puo_piu_cambiare_le_date_di_un_piano_con_fasi()
    {
        var (ctx, consegna) = Scenario();
        using var _ = ctx;

        var modifica = new Commessa
        {
            Id = 1,
            Code = "CM-TEST-0001",
            IdCompany = 1,
            Name = "Rinominata",
            State = CommessaStates.Planned,
            StartDatePlanned = DateTime.Today.NextWorkday(),
            EndDatePlanned = consegna.AddWorkdays(10)
        };

        var resp = await Servizio(ctx).PostAsync(modifica);

        Assert.False(resp.State);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, resp.Code);
        Assert.Equal(consegna, ctx.RileggiCommessa().EndDatePlanned);
    }
}
