using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CRM.Tests;

/// <summary>
/// Fine e scadenza di un ticket di produzione sono quelle della fase che esegue: qui si verifica
/// che restino tali anche quando la fase si sposta (modifica diretta, cascata sui successori,
/// riprogrammazione in blocco) e che i ticket chiusi restino con le loro date storiche.
/// </summary>
public class PhaseTicketDeadlineTests
{
    private static CommessaFaseDTO Dto(CommessaFase f, DateTime? start = null, DateTime? end = null)
        => new()
        {
            Id = f.Id,
            IdCommessa = f.IdCommessa,
            Name = f.Name,
            StartDate = start ?? f.StartDate,
            EndDate = end ?? f.EndDate,
            SortOrder = f.SortOrder,
            Progress = f.Progress,
            State = f.State,
            CompletionMode = f.CompletionMode,
            RequiresTicket = f.RequiresTicket,
            AutoCreateTicketOnTake = f.AutoCreateTicketOnTake
        };

    private static Ticket RileggiTicket(ProductionTestContext ctx, int id)
    {
        ctx.Db.ChangeTracker.Clear();
        return ctx.Db.Tickets.AsNoTracking().Single(t => t.Id == id);
    }

    [Fact]
    public async Task Spostare_la_fase_sposta_fine_e_scadenza_dei_suoi_ticket_aperti()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.DateExpired = fase.EndDate;
        ticket.DateEnd = fase.EndDate;
        ctx.Db.SaveChanges();

        var nuovaFine = fase.EndDate.AddDays(7);
        Assert.True((await ctx.Service.SaveAsync(Dto(fase, end: nuovaFine))).State);

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(nuovaFine.Date, aggiornato.DateExpired);
        Assert.Equal(nuovaFine.Date, aggiornato.DateEnd);
    }

    /// <summary>La fine dell'appuntamento cambia giorno, non ora: chi ha pianificato fino alle
    /// 17:30 se la ritrova alle 17:30 del nuovo giorno, non a mezzanotte.</summary>
    [Fact]
    public async Task La_fine_dell_appuntamento_conserva_l_ora()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.DateEnd = fase.EndDate.Date.AddHours(17).AddMinutes(30);
        ctx.Db.SaveChanges();

        var nuovaFine = fase.EndDate.AddDays(7);
        Assert.True((await ctx.Service.SaveAsync(Dto(fase, end: nuovaFine))).State);

        Assert.Equal(nuovaFine.Date.AddHours(17).AddMinutes(30), RileggiTicket(ctx, 10).DateEnd);
    }

    /// <summary>Fase tirata indietro: l'inizio pianificato non puo' restare dopo la fine.</summary>
    [Fact]
    public async Task Se_la_fase_finisce_prima_dell_inizio_anche_l_inizio_torna_indietro()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.Date = fase.EndDate.Date.AddHours(9);
        ticket.DateEnd = fase.EndDate.Date.AddHours(18);
        ticket.ReminderApptStatus = ReminderStatus.Sent;
        ctx.Db.SaveChanges();

        var nuovaFine = fase.EndDate.AddDays(-10);
        Assert.True((await ctx.Service.SaveAsync(Dto(fase, start: fase.StartDate.AddDays(-10), end: nuovaFine))).State);

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(nuovaFine.Date.AddHours(9), aggiornato.Date);      // l'ora dell'inizio resta
        Assert.Equal(nuovaFine.Date.AddHours(18), aggiornato.DateEnd);
        Assert.Equal(ReminderStatus.Pending, aggiornato.ReminderApptStatus);
    }

    /// <summary>Fase spostata in avanti: l'inizio resta dov'e', non c'e' nulla da correggere.</summary>
    [Fact]
    public async Task Spostando_la_fase_in_avanti_l_inizio_resta_dov_e()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        var inizio = fase.StartDate.Date.AddHours(9);
        ticket.Date = inizio;
        ticket.ReminderApptStatus = ReminderStatus.Sent;
        ctx.Db.SaveChanges();

        Assert.True((await ctx.Service.SaveAsync(Dto(fase, end: fase.EndDate.AddDays(7)))).State);

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(inizio, aggiornato.Date);
        Assert.Equal(ReminderStatus.Sent, aggiornato.ReminderApptStatus);
    }

    [Fact]
    public async Task Le_date_di_un_ticket_chiuso_restano_quelle_storiche()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var dateOriginali = fase.EndDate.Date;
        var ticket = ctx.CreaTicket(10, idFase: 1, chiuso: true);
        ticket.DateExpired = dateOriginali;
        ticket.DateEnd = dateOriginali;
        ctx.Db.SaveChanges();

        Assert.True((await ctx.Service.SaveAsync(Dto(fase, end: fase.EndDate.AddDays(7)))).State);

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(dateOriginali, aggiornato.DateExpired);
        Assert.Equal(dateOriginali, aggiornato.DateEnd);
    }

    [Fact]
    public async Task La_cascata_sui_successori_trascina_anche_le_loro_scadenze()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var prima = ctx.CreaFase(1, nome: "Prima");
        var seconda = ctx.CreaFase(2, nome: "Seconda");
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);

        var ticket = ctx.CreaTicket(20, idFase: 2);
        ticket.DateExpired = seconda.EndDate.Date;
        ctx.Db.SaveChanges();

        // Le entita' del contesto sono le stesse che la cascata modifica in memoria: la scadenza
        // di partenza va copiata prima, o il confronto finale sarebbe con il valore gia' spostato.
        var scadenzaIniziale = seconda.EndDate.Date;

        // La prima fase finisce molto piu' avanti: la seconda deve slittare, e con lei il ticket.
        Assert.True((await ctx.Service.SaveAsync(Dto(prima, end: prima.EndDate.AddDays(30)))).State);

        var secondaAggiornata = ctx.Rileggi(2);
        Assert.True(secondaAggiornata.EndDate.Date > scadenzaIniziale, "la cascata non ha spostato il successore");
        var ticketAggiornato = RileggiTicket(ctx, 20);
        Assert.Equal(secondaAggiornata.EndDate.Date, ticketAggiornato.DateExpired);
        Assert.Equal(secondaAggiornata.EndDate.Date, ticketAggiornato.DateEnd);
    }

    [Fact]
    public async Task Il_salvataggio_massivo_allinea_fine_e_scadenza()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.DateExpired = fase.EndDate.Date;
        ticket.DateEnd = fase.EndDate.Date;
        ctx.Db.SaveChanges();

        var nuovaFine = fase.EndDate.AddDays(3);
        Assert.True(await ctx.Service.BulkSaveAsync(new List<CommessaFaseDTO>
        {
            Dto(fase, start: fase.StartDate.AddDays(3), end: nuovaFine)
        }));

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(nuovaFine.Date, aggiornato.DateExpired);
        Assert.Equal(nuovaFine.Date, aggiornato.DateEnd);
    }

    [Fact]
    public async Task Cambiare_scadenza_rimette_in_coda_il_preavviso()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.DateExpired = fase.EndDate.Date;
        // Preavviso gia' inviato per la vecchia scadenza: se restasse Sent, quello per la
        // nuova data non partirebbe mai.
        ticket.ReminderExpiryStatus = ReminderStatus.Sent;
        ticket.ReminderExpiryRetryCount = 2;
        ticket.ReminderExpiryLastAttemptAt = DateTime.Now.AddDays(-1);
        ctx.Db.SaveChanges();

        Assert.True((await ctx.Service.SaveAsync(Dto(fase, end: fase.EndDate.AddDays(7)))).State);

        var aggiornato = RileggiTicket(ctx, 10);
        Assert.Equal(ReminderStatus.Pending, aggiornato.ReminderExpiryStatus);
        Assert.Equal(0, aggiornato.ReminderExpiryRetryCount);
        Assert.Null(aggiornato.ReminderExpiryLastAttemptAt);
    }

    [Fact]
    public async Task Una_fase_ferma_non_tocca_il_preavviso_gia_inviato()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1);
        var ticket = ctx.CreaTicket(10, idFase: 1);
        ticket.DateExpired = fase.EndDate.Date;
        ticket.ReminderExpiryStatus = ReminderStatus.Sent;
        ctx.Db.SaveChanges();

        // Salvataggio che non tocca le date: il preavviso non va rimesso in coda.
        var dto = Dto(fase);
        dto.Name = "Rinominata";
        Assert.True((await ctx.Service.SaveAsync(dto)).State);

        Assert.Equal(ReminderStatus.Sent, RileggiTicket(ctx, 10).ReminderExpiryStatus);
    }

    [Fact]
    public async Task Il_ticket_generato_dal_piano_nasce_con_le_date_della_fase()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idTicketType: 1);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);

        var resp = await ctx.Service.GenerateTicketFromPlanAsync(piano.Id);

        Assert.True(resp.State);
        ctx.Db.ChangeTracker.Clear();
        var ticket = ctx.Db.Tickets.AsNoTracking().Single(t => t.IdCommessaFase == 1);
        Assert.Equal(fase.EndDate.Date, ticket.DateExpired);
        Assert.Equal(fase.EndDate.Date, ticket.DateEnd);
    }
}
