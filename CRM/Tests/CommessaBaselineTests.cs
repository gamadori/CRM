using CRM.Client.Services;
using CRM.Server.Extensions;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// La baseline della commessa: la consegna promessa all'avvio e le ore stimate, congelate perché
/// il consuntivo abbia un termine di paragone. Il difetto che questi test difendono è preciso —
/// se la promessa si muovesse insieme alla consegna operativa, a fine lavoro ogni commessa
/// risulterebbe puntuale, comprese quelle riprogrammate cinque volte.
/// </summary>
public class CommessaBaselineTests
{
    private static CommesseService Servizio(ProductionTestContext ctx)
        => new(ctx.Db, ctx.Permits, Substitute.For<ILogEventService>());

    [Fact]
    public async Task Riprogrammare_sposta_la_consegna_ma_non_la_promessa()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        var commessa = ctx.CreaCommessa();
        var consegna = DateTime.Today.AddDays(30).PreviousWorkday();
        commessa.EndDatePlanned = consegna;
        commessa.EndDateBaseline = consegna;
        commessa.StartDatePlanned = DateTime.Today.NextWorkday();
        ctx.Db.SaveChanges();
        ctx.CreaFase(1, nome: "Lavorazione");

        var resp = await Servizio(ctx).RescheduleAsync(1, consegna.AddWorkdays(10));

        Assert.True(resp.State);
        var dopo = ctx.RileggiCommessa();
        Assert.Equal(consegna.AddWorkdays(10), dopo.EndDatePlanned);
        Assert.Equal(consegna, dopo.EndDateBaseline);
    }

    [Fact]
    public async Task Salvare_la_scheda_non_riscrive_la_baseline()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        var commessa = ctx.CreaCommessa();
        commessa.EndDateBaseline = DateTime.Today.AddDays(30);
        commessa.BudgetHours = 240;
        commessa.BudgetHoursBaseline = 240;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        // Le ore a budget si correggono in corsa: è la stima iniziale che non deve seguirle.
        var resp = await Servizio(ctx).PostAsync(new Commessa
        {
            Id = 1,
            IdCompany = 1,
            Name = "Sviluppo",
            BudgetHours = 400,
            StartDatePlanned = commessa.StartDatePlanned,
            EndDatePlanned = commessa.EndDatePlanned,
            State = CommessaStates.InProgress
        });

        Assert.True(resp.State);
        var dopo = ctx.RileggiCommessa();
        Assert.Equal(400, dopo.BudgetHours);
        Assert.Equal(240, dopo.BudgetHoursBaseline);
        Assert.Equal(DateTime.Today.AddDays(30), dopo.EndDateBaseline);
    }

    [Fact]
    public void Senza_baseline_lo_scostamento_non_esiste()
    {
        var dto = new CommessaDTO { EndDateActual = DateTime.Today };

        Assert.False(dto.HasBaseline);
        Assert.Null(dto.DeliveryDeltaDays);
        Assert.Null(dto.HoursDeltaPct);
    }

    [Fact]
    public void Lo_scostamento_consegna_confronta_la_promessa_con_la_fine_effettiva()
    {
        var dto = new CommessaDTO
        {
            EndDateBaseline = new DateTime(2026, 11, 30),
            EndDatePlanned = new DateTime(2026, 12, 20), // riprogrammata: non deve entrare nel conto
            EndDateActual = new DateTime(2026, 12, 15)
        };

        Assert.Equal(15, dto.DeliveryDeltaDays);
    }

    /// <summary>
    /// Da aperta, lo scostamento guarda dove il piano dice che si andra' a finire, non che giorno e'
    /// oggi. Prima usava oggi, e su una commessa appena avviata usciva un "anticipo" che era solo il
    /// conto alla rovescia alla promessa.
    /// </summary>
    [Fact]
    public void Finche_la_commessa_e_aperta_si_confronta_con_la_fine_prevista()
    {
        var dto = new CommessaDTO
        {
            EndDateBaseline = new DateTime(2026, 11, 30),
            EndDatePlanned = new DateTime(2026, 11, 30),
            ExpectedEndDate = new DateTime(2026, 12, 4)
        };

        Assert.Equal(4, dto.DeliveryDeltaDays);
    }

    /// <summary>Senza fasi non c'e' una previsione: si ripiega sulla consegna pianificata.</summary>
    [Fact]
    public void Senza_fine_prevista_si_usa_la_consegna_pianificata()
    {
        var dto = new CommessaDTO
        {
            EndDateBaseline = new DateTime(2026, 11, 30),
            EndDatePlanned = new DateTime(2026, 12, 10)
        };

        Assert.Equal(10, dto.DeliveryDeltaDays);
    }

    [Fact]
    public void Le_ore_si_confrontano_con_la_stima_iniziale()
    {
        var dto = new CommessaDTO
        {
            BudgetHoursBaseline = 240,
            BudgetHours = 400, // budget rivisto in corsa: fuori dal conto
            SpentMinutes = 310 * 60
        };

        Assert.Equal(310m, dto.SpentHours);
        Assert.Equal(29, dto.HoursDeltaPct);
    }

    [Fact]
    public void Una_stima_a_zero_non_produce_una_percentuale()
    {
        var dto = new CommessaDTO { BudgetHoursBaseline = 0, SpentMinutes = 600 };

        Assert.Null(dto.HoursDeltaPct);
    }
}
