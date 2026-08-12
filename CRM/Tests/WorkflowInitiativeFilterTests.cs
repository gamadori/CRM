using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Il vincolo di iniziativa sulle regole di automazione. Serve perche' la fonte "Evento" dice che
/// un lead arriva da un evento, non da QUALE: senza vincolo la regola scritta per il richiamo dei
/// biglietti di una fiera scatta anche sui lead di un webinar.
/// <para>
/// Le due cose da tenere ferme sono simmetriche: la regola ristretta non deve uscire dalla sua
/// iniziativa, e la regola senza vincolo - cioe' ogni regola gia' esistente il giorno in cui il
/// campo e' comparso - deve continuare a valere per tutti.
/// </para>
/// </summary>
public class WorkflowInitiativeFilterTests
{
    private static WorkflowAutomationService Motore(InitiativeTestContext ctx)
    {
        var permits = Substitute.For<IPermitsService>();
        permits.IdUser().Returns(InitiativeTestContext.Utente);

        return new WorkflowAutomationService(ctx.Db, permits, Substitute.For<ILogEventService>());
    }

    private static WorkflowAutomation Regola(int? idInitiative) => new()
    {
        Name = "Richiamo lead fiera",
        IsActive = true,
        Trigger = WorkflowTrigger.LeadCreated,
        LeadStatus = LeadStatus.New,
        LeadSource = LeadSource.Event,
        IdInitiative = idInitiative,
        ActivityKind = ActivityKind.Call,
        ActivitySubject = "Richiamare {LeadName}",
        DueDays = 5,
        AssignToOwner = true,
        CreatedAt = DateTime.Now.AddHours(-1)
    };

    private static Lead Biglietto(string nome, int? idInitiative) => new()
    {
        Name = nome,
        Status = LeadStatus.New,
        Source = LeadSource.Event,
        IdInitiative = idInitiative,
        IdUser = InitiativeTestContext.Utente,
        CreatedAt = DateTime.Now
    };

    private static async Task<(int Fiera, int Webinar)> DueIniziativeAsync(InitiativeTestContext ctx)
    {
        var fiera = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(InitiativeKind.Fair));
        var webinar = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(InitiativeKind.Webinar));
        return (fiera.Data!.Id, webinar.Data!.Id);
    }

    [Fact]
    public async Task La_regola_ristretta_a_una_fiera_non_tocca_gli_altri_lead()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var (fiera, webinar) = await DueIniziativeAsync(ctx);

        ctx.Db.WorkflowAutomations.Add(Regola(fiera));
        ctx.Db.Leads.AddRange(
            Biglietto("Rossi in fiera", fiera),
            Biglietto("Bianchi al webinar", webinar),
            Biglietto("Verdi senza iniziativa", null));
        await ctx.Db.SaveChangesAsync();

        await Motore(ctx).ExecutePendingAsync(50);

        var attivita = await ctx.Db.Activities.AsNoTracking().ToListAsync();
        var lead = Assert.Single(attivita);
        Assert.Equal("Richiamare Rossi in fiera", lead.Subject);
        Assert.Equal(ActivityKind.Call, lead.Kind);
        Assert.Equal(DateTime.Today.AddDays(5), lead.DueDate);
    }

    [Fact]
    public async Task Senza_vincolo_la_regola_vale_per_tutti_come_prima()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var (fiera, webinar) = await DueIniziativeAsync(ctx);

        ctx.Db.WorkflowAutomations.Add(Regola(idInitiative: null));
        ctx.Db.Leads.AddRange(
            Biglietto("Rossi in fiera", fiera),
            Biglietto("Bianchi al webinar", webinar),
            Biglietto("Verdi senza iniziativa", null));
        await ctx.Db.SaveChangesAsync();

        await Motore(ctx).ExecutePendingAsync(50);

        Assert.Equal(3, await ctx.Db.Activities.CountAsync());
    }

    [Fact]
    public async Task Lo_stesso_lead_non_produce_due_attivita()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var (fiera, _) = await DueIniziativeAsync(ctx);

        ctx.Db.WorkflowAutomations.Add(Regola(fiera));
        ctx.Db.Leads.Add(Biglietto("Rossi in fiera", fiera));
        await ctx.Db.SaveChangesAsync();

        var motore = Motore(ctx);
        await motore.ExecutePendingAsync(50);
        await motore.ExecutePendingAsync(50);

        Assert.Equal(1, await ctx.Db.Activities.CountAsync());
    }
}
