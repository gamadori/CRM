using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Il resoconto di un'iniziativa. Due regole non negoziabili: il ritorno commerciale si calcola
/// solo dove e' una domanda sensata, e una spesa non convertita non entra nei totali ne' sparisce.
/// </summary>
public class InitiativeReportTests
{
    private static async Task<int> CreaAsync(InitiativeTestContext ctx, InitiativeKind kind)
    {
        var response = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(kind));
        return response.Data!.Id;
    }

    private static void AggiungiSpesa(InitiativeTestContext ctx, int idInitiative, decimal? importoBase)
    {
        ctx.Db.ExpenseReceipts.Add(new ExpenseReceipt
        {
            IdInitiative = idInitiative,
            IdUserSpender = InitiativeTestContext.Utente,
            TotalAmount = 100,
            AmountBase = importoBase,
            TransactionDate = DateTime.Today
        });

        ctx.Db.SaveChanges();
    }

    [Fact]
    public async Task Sulla_trasferta_il_ritorno_non_si_calcola()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Trip);

        AggiungiSpesa(ctx, id, 500);

        var report = await ctx.Service.GetReportAsync(id);

        Assert.NotNull(report);
        Assert.Equal(500, report!.CostTotal);

        // I clienti c'erano gia': un ROI qui sarebbe un numero preciso e falso.
        Assert.Null(report.Roi);
        Assert.Null(report.CostPerLead);
    }

    [Fact]
    public async Task Sulla_fiera_il_costo_per_lead_si_calcola_sui_lead_raccolti()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Fair);

        AggiungiSpesa(ctx, id, 600);

        ctx.Db.Leads.AddRange(
            new Lead { Name = "Primo", IdInitiative = id, Source = LeadSource.Event },
            new Lead { Name = "Secondo", IdInitiative = id, Source = LeadSource.Event },
            new Lead { Name = "Terzo", IdInitiative = id, Source = LeadSource.Event });

        ctx.Db.SaveChanges();

        var report = await ctx.Service.GetReportAsync(id);

        Assert.Equal(3, report!.LeadTotal);
        Assert.Equal(200, report.CostPerLead);
    }

    [Fact]
    public async Task Senza_lead_il_costo_per_lead_resta_vuoto_invece_di_dividere_per_zero()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Fair);

        AggiungiSpesa(ctx, id, 600);

        var report = await ctx.Service.GetReportAsync(id);

        Assert.Equal(0, report!.LeadTotal);
        Assert.Null(report.CostPerLead);
    }

    [Fact]
    public async Task Una_spesa_non_convertita_resta_fuori_dal_totale_ma_viene_contata()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Trip);

        AggiungiSpesa(ctx, id, 300);
        AggiungiSpesa(ctx, id, null);

        var report = await ctx.Service.GetReportAsync(id);

        Assert.Equal(300, report!.CostTotal);
        Assert.Equal(2, report.ExpenseCount);

        // Il punto: non entra nei totali e non sparisce. Un consuntivo che tace una spesa vale
        // meno di un consuntivo mancante.
        Assert.Equal(1, report.ExpensePendingConversion);
    }

    [Fact]
    public async Task Le_opportunita_della_fiera_si_leggono_dall_attribuzione_diretta()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Fair);

        ctx.Db.Deals.AddRange(
            new Deal { Name = "Vinta", IdInitiative = id, Amount = 1000, State = DealStates.CloseWon, IdCompany = 1 },
            new Deal { Name = "Aperta", IdInitiative = id, Amount = 500, State = DealStates.Open, IdCompany = 1 });

        ctx.Db.SaveChanges();

        var report = await ctx.Service.GetReportAsync(id);

        Assert.Equal(2, report!.DealCount);
        Assert.Equal(1500, report.DealAmount);
        Assert.Equal(1, report.DealWonCount);
        Assert.Equal(1000, report.DealWonAmount);
    }

    [Fact]
    public async Task Le_opportunita_della_trasferta_si_leggono_dall_attivita_di_origine()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeKind.Trip);

        var visita = new Activity
        {
            Subject = "Visita da Muller",
            EntityType = ActivityEntityType.Company,
            EntityId = 7,
            IdInitiative = id,
            State = ActivityState.Done,
            DoneDate = DateTime.Today
        };

        ctx.Db.Activities.Add(visita);
        ctx.Db.SaveChanges();

        // Attribuzione INDIRETTA: sul viaggio l'opportunita' appartiene al cliente, non al giro,
        // e ci si arriva passando dalla visita.
        ctx.Db.Deals.Add(new Deal
        {
            Name = "Nata dalla visita",
            IdActivityOrigin = visita.Id,
            Amount = 800,
            State = DealStates.Open,
            IdCompany = 7
        });

        ctx.Db.SaveChanges();

        var report = await ctx.Service.GetReportAsync(id);

        Assert.Equal(1, report!.DealCount);
        Assert.Equal(800, report.DealAmount);
        Assert.Single(report.Visits);
    }
}
