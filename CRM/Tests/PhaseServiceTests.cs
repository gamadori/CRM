using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace CRM.Tests;

/// <summary>
/// Regole che vivono nel servizio e passano dal database: chi può toccare una fase, quando è
/// avviabile, cosa succede quando si chiude. Sono i bug corretti (gruppo, stato riportato indietro,
/// dipendenze non vincolanti) più l'automazione dei ticket previsti.
/// </summary>
public class PhaseServiceTests
{
    private static CommessaFaseDTO Dto(CommessaFase f, int? progress = null, CommessaFaseStates? stato = null)
        => new()
        {
            Id = f.Id,
            IdCommessa = f.IdCommessa,
            Name = f.Name,
            StartDate = f.StartDate,
            EndDate = f.EndDate,
            SortOrder = f.SortOrder,
            Progress = progress ?? f.Progress,
            State = stato ?? f.State,
            CompletionMode = f.CompletionMode,
            RequiresTicket = f.RequiresTicket,
            AutoCreateTicketOnTake = f.AutoCreateTicketOnTake,
            // Gruppo e tipo ticket fanno parte del DTO: da quando l'editor li espone, il
            // salvataggio li scrive, e mandarne uno senza equivarrebbe a cancellarli.
            IdGroup = f.IdGroup,
            IdTicketType = f.IdTicketType
        };

    // ─── Permessi di gruppo ──────────────────────────────────────────────────

    [Fact]
    public async Task Chi_non_e_del_gruppo_non_puo_modificare_la_fase()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(idGruppo: 99);
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: 5);

        var resp = await ctx.Service.SaveAsync(Dto(fase));

        Assert.False(resp.State);
        Assert.Equal(HttpStatusCode.Forbidden, resp.Code);
    }

    [Fact]
    public async Task Chi_e_del_gruppo_puo_modificare_la_fase()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(idGruppo: 5);
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: 5);

        var dto = Dto(fase);
        dto.Name = "Rinominata";
        var resp = await ctx.Service.SaveAsync(dto);

        Assert.True(resp.State);
        Assert.Equal("Rinominata", ctx.Rileggi(1).Name);
    }

    [Fact]
    public async Task Admin_non_e_vincolato_dal_gruppo()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: 5);

        Assert.True((await ctx.Service.SaveAsync(Dto(fase))).State);
    }

    [Fact]
    public async Task Una_fase_senza_gruppo_non_pone_vincoli()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(idGruppo: null);
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: null);

        Assert.True((await ctx.Service.SaveAsync(Dto(fase))).State);
    }

    // ─── Gruppo e tipo ticket ────────────────────────────────────────────────

    /// <summary>
    /// Da quando l'editor delle fasi espone gruppo e tipo ticket, il DTO comanda: una fase nata
    /// senza tipo (commessa aperta, fase aggiunta a mano) altrimenti non avrebbe mai potuto aprire
    /// ticket. Il rovescio è che ora chi chiama deve mandare il DTO completo — prima quei due campi
    /// venivano ignorati apposta, perché nessun editor li rispediva.
    /// </summary>
    [Fact]
    public async Task Gruppo_e_tipo_ticket_si_aggiornano_dal_dto()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: null, idTicketType: null);

        var dto = Dto(fase);
        dto.IdGroup = 5;
        dto.IdTicketType = 7;

        Assert.True((await ctx.Service.SaveAsync(dto)).State);

        var salvata = ctx.Rileggi(1);
        Assert.Equal(5, salvata.IdGroup);
        Assert.Equal(7, salvata.IdTicketType);
    }

    /// <summary>
    /// Il gruppo si può cambiare solo verso uno di cui si fa parte: stessa regola della creazione.
    /// Senza, bastava riassegnare la fase a un reparto qualsiasi per uscire dal proprio perimetro.
    /// </summary>
    [Fact]
    public async Task Non_si_sposta_una_fase_a_un_gruppo_di_cui_non_si_fa_parte()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(3);
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, idGruppo: 3);

        var dto = Dto(fase);
        dto.IdGroup = 9; // gruppo altrui

        var resp = await ctx.Service.SaveAsync(dto);

        Assert.False(resp.State);
        Assert.Equal(3, ctx.Rileggi(1).IdGroup);
    }

    /// <summary>Regressione: lo stato arrivava dal DTO, quindi tornava a Pending.</summary>
    [Fact]
    public async Task Un_dto_con_stato_iniziale_non_riporta_indietro_una_fase_avviata()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, stato: CommessaFaseStates.InProgress);
        ctx.CreaTicket(100, idFase: 1, chiuso: false);

        await ctx.Service.SaveAsync(Dto(fase, progress: 0, stato: CommessaFaseStates.Pending));

        Assert.Equal(CommessaFaseStates.InProgress, ctx.Rileggi(1).State);
    }

    // ─── Dipendenze vincolanti ───────────────────────────────────────────────

    [Fact]
    public async Task Non_si_completa_a_mano_una_fase_con_predecessori_aperti()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio", mode: CommessaFaseCompletionMode.Manual);
        var seconda = ctx.CreaFase(2, nome: "Verniciatura", mode: CommessaFaseCompletionMode.Manual);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);

        var resp = await ctx.Service.SaveAsync(Dto(seconda, progress: 100));

        Assert.False(resp.State);
        Assert.Equal(HttpStatusCode.Conflict, resp.Code);
        Assert.Contains("Taglio", resp.Message);           // dice quale fase blocca
        Assert.Equal(CommessaFaseStates.Pending, ctx.Rileggi(2).State);
    }

    [Fact]
    public async Task Con_i_predecessori_conclusi_la_fase_si_completa()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, stato: CommessaFaseStates.Done, mode: CommessaFaseCompletionMode.Manual, progress: 100);
        var seconda = ctx.CreaFase(2, mode: CommessaFaseCompletionMode.Manual);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);

        var resp = await ctx.Service.SaveAsync(Dto(seconda, progress: 100));

        Assert.True(resp.State);
        Assert.Equal(CommessaFaseStates.Done, ctx.Rileggi(2).State);
    }

    /// <summary>Il vincolo riguarda l'avanzamento: ripianificare deve restare libero.</summary>
    [Fact]
    public async Task Si_puo_ripianificare_una_fase_bloccata()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1);
        var seconda = ctx.CreaFase(2);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);

        var dto = Dto(seconda);
        dto.Name = "Nuovo nome";
        dto.StartDate = DateTime.Today.AddDays(10);
        dto.EndDate = DateTime.Today.AddDays(14);

        var resp = await ctx.Service.SaveAsync(dto);

        Assert.True(resp.State);
        Assert.Equal("Nuovo nome", ctx.Rileggi(2).Name);
    }

    [Fact]
    public async Task I_bloccanti_elencano_solo_i_predecessori_non_conclusi()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio", stato: CommessaFaseStates.Done);
        ctx.CreaFase(2, nome: "Saldatura", stato: CommessaFaseStates.InProgress);
        ctx.CreaFase(3, nome: "Montaggio");
        ctx.CreaDipendenza(idFase: 3, idPredecessore: 1);
        ctx.CreaDipendenza(idFase: 3, idPredecessore: 2);

        var bloccanti = await ctx.Service.GetStartBlockersAsync(3);

        Assert.Equal(new[] { "Saldatura" }, bloccanti);
    }

    /// <summary>Una fase già avviata non si ricongela se un predecessore torna aperto.</summary>
    [Fact]
    public async Task Una_fase_gia_avviata_non_ha_bloccanti()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio", stato: CommessaFaseStates.InProgress);
        ctx.CreaFase(2, stato: CommessaFaseStates.InProgress);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);

        Assert.Empty(await ctx.Service.GetStartBlockersAsync(2));
    }

    // ─── Richiede ticket ─────────────────────────────────────────────────────

    [Fact]
    public async Task Senza_ticket_chiusi_la_fase_che_li_richiede_non_si_completa()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, mode: CommessaFaseCompletionMode.Manual, requiresTicket: true);

        var resp = await ctx.Service.SaveAsync(Dto(fase, progress: 100));

        Assert.False(resp.State);
        Assert.Equal(HttpStatusCode.Conflict, resp.Code);
    }

    [Fact]
    public async Task Con_un_ticket_chiuso_la_fase_che_li_richiede_si_completa()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var fase = ctx.CreaFase(1, mode: CommessaFaseCompletionMode.Manual, requiresTicket: true);
        ctx.CreaTicket(100, idFase: 1, chiuso: true);

        var resp = await ctx.Service.SaveAsync(Dto(fase, progress: 100));

        Assert.True(resp.State);
        Assert.Equal(CommessaFaseStates.Done, ctx.Rileggi(1).State);
    }

    // ─── Generazione del ticket dal piano ────────────────────────────────────

    [Fact]
    public async Task Generare_il_ticket_avvia_la_fase_e_registra_chi_l_ha_presa()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);

        var resp = await ctx.Service.GenerateTicketFromPlanAsync(piano.Id);

        Assert.True(resp.State);
        var fase = ctx.Rileggi(1);
        Assert.Equal(CommessaFaseStates.InProgress, fase.State);
        Assert.Equal(ProductionTestContext.Utente, fase.IdUserTakenBy);
        Assert.NotNull(fase.TakenAt);
        Assert.Single(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 1));
    }

    [Fact]
    public async Task Non_si_generano_ticket_su_una_fase_bloccata_dai_predecessori()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio");
        ctx.CreaFase(2);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);
        var piano = ctx.CreaPianoTicket(1, idFase: 2);

        var resp = await ctx.Service.GenerateTicketFromPlanAsync(piano.Id);

        Assert.False(resp.State);
        Assert.Equal(HttpStatusCode.Conflict, resp.Code);
        Assert.Empty(ctx.Db.Tickets);
    }

    [Fact]
    public async Task Non_si_generano_ticket_su_una_fase_di_un_altro_gruppo()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(idGruppo: 99);
        ctx.CreaCommessa();
        ctx.CreaFase(1, idGruppo: 5);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);

        var resp = await ctx.Service.GenerateTicketFromPlanAsync(piano.Id);

        Assert.False(resp.State);
        Assert.Equal(HttpStatusCode.Forbidden, resp.Code);
        Assert.Empty(ctx.Db.Tickets);
    }

    // ─── Automazione "a inizio fase" ─────────────────────────────────────────

    /// <summary>
    /// Il comportamento nuovo: chiusa la fase precedente, i ticket previsti della successiva
    /// nascono da soli. Prima la modalità OnPhaseStart era indistinguibile da Manual.
    /// </summary>
    [Fact]
    public async Task Chiudere_una_fase_genera_i_ticket_previsti_della_successiva()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio");
        ctx.CreaFase(2, nome: "Verniciatura");
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);
        var piano = ctx.CreaPianoTicket(10, idFase: 2, mode: ProductionTicketAutoCreateMode.OnPhaseStart);

        // Il ticket della prima fase viene chiuso: la fase si conclude.
        ctx.CreaTicket(100, idFase: 1, chiuso: true);
        await ctx.Service.RecomputeFaseProgressAsync(1);

        Assert.Equal(CommessaFaseStates.Done, ctx.Rileggi(1).State);

        ctx.Db.ChangeTracker.Clear();
        var pianoAggiornato = ctx.Db.CommessaFaseTicketPlans.AsNoTracking().Single(p => p.Id == piano.Id);
        Assert.NotNull(pianoAggiornato.IdTicket);
        Assert.Single(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 2));
    }

    [Fact]
    public async Task Non_si_generano_ticket_se_restano_altri_predecessori_aperti()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Taglio");
        ctx.CreaFase(2, nome: "Saldatura");
        ctx.CreaFase(3, nome: "Verniciatura");
        ctx.CreaDipendenza(idFase: 3, idPredecessore: 1);
        ctx.CreaDipendenza(idFase: 3, idPredecessore: 2);   // resta aperta
        ctx.CreaPianoTicket(10, idFase: 3);

        ctx.CreaTicket(100, idFase: 1, chiuso: true);
        await ctx.Service.RecomputeFaseProgressAsync(1);

        Assert.Empty(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 3));
    }

    [Fact]
    public async Task I_piani_manuali_non_vengono_generati_da_soli()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1);
        ctx.CreaFase(2);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);
        ctx.CreaPianoTicket(10, idFase: 2, mode: ProductionTicketAutoCreateMode.Manual);

        ctx.CreaTicket(100, idFase: 1, chiuso: true);
        await ctx.Service.RecomputeFaseProgressAsync(1);

        Assert.Empty(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 2));
    }

    [Fact]
    public async Task La_generazione_automatica_non_duplica_i_ticket()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1);
        ctx.CreaFase(2);
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);
        ctx.CreaPianoTicket(10, idFase: 2);

        ctx.CreaTicket(100, idFase: 1, chiuso: true);
        await ctx.Service.RecomputeFaseProgressAsync(1);
        await ctx.Service.RecomputeFaseProgressAsync(1);   // secondo evento sullo stesso ticket

        Assert.Single(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 2));
    }

    /// <summary>
    /// La generazione automatica è il sistema che esegue il template: non deve dipendere dal
    /// gruppo di chi ha chiuso il ticket precedente, che quasi mai è quello della fase successiva.
    /// </summary>
    [Fact]
    public async Task La_generazione_automatica_non_dipende_dal_gruppo_di_chi_chiude()
    {
        using var ctx = ProductionTestContext.ComeUtenteDelGruppo(idGruppo: 1);
        ctx.CreaCommessa();
        ctx.CreaFase(1, idGruppo: 1);
        ctx.CreaFase(2, idGruppo: 42);           // reparto diverso
        ctx.CreaDipendenza(idFase: 2, idPredecessore: 1);
        ctx.CreaPianoTicket(10, idFase: 2);

        ctx.CreaTicket(100, idFase: 1, chiuso: true);
        await ctx.Service.RecomputeFaseProgressAsync(1);

        Assert.Single(ctx.Db.Tickets.Where(t => t.IdCommessaFase == 2));
    }

    // ─── Raggruppamenti e avanzamento di commessa ────────────────────────────

    [Fact]
    public async Task Il_padre_prende_la_media_pesata_dei_figli()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        // Partenza di lunedi': le durate sono in giorni di calendario ma i pesi in giorni
        // lavorativi, quindi senza ancora il risultato dipende dal giorno in cui gira il test.
        var lunedi = ProductionTestContext.ProssimoLunedi;
        ctx.CreaFase(1, nome: "Assemblaggio", giorniDurata: 12, inizio: lunedi);
        ctx.CreaFase(2, parentId: 1, mode: CommessaFaseCompletionMode.Manual, progress: 100, giorniDurata: 10, inizio: lunedi);
        ctx.CreaFase(3, parentId: 1, mode: CommessaFaseCompletionMode.Manual, progress: 0, giorniDurata: 2, inizio: lunedi);

        await ctx.Service.RecomputeFaseProgressAsync(2);

        // Le durate sono pesate sui giorni lavorativi: 8 giorni al 100% e 2 allo 0% = 80.
        Assert.Equal(80, ctx.Rileggi(1).Progress);
        Assert.Equal(CommessaFaseStates.InProgress, ctx.Rileggi(1).State);
    }

    [Fact]
    public async Task L_avanzamento_della_commessa_conta_solo_le_foglie()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        var lunedi = ProductionTestContext.ProssimoLunedi;
        ctx.CreaFase(1, nome: "Assemblaggio", giorniDurata: 12, inizio: lunedi);
        ctx.CreaFase(2, parentId: 1, mode: CommessaFaseCompletionMode.Manual, progress: 100, giorniDurata: 10, inizio: lunedi);
        ctx.CreaFase(3, parentId: 1, mode: CommessaFaseCompletionMode.Manual, progress: 0, giorniDurata: 2, inizio: lunedi);

        await ctx.Service.RecomputeFaseProgressAsync(2);

        Assert.Equal(80, ctx.RileggiCommessa().Progress);
    }

    [Fact]
    public async Task Il_padre_e_concluso_solo_quando_lo_sono_tutti_i_figli()
    {
        using var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        ctx.CreaFase(1, nome: "Assemblaggio", giorniDurata: 10);
        // Entrambi i figli conclusi: stato e percentuale coerenti, come nei dati reali.
        ctx.CreaFase(2, parentId: 1, mode: CommessaFaseCompletionMode.Manual,
            progress: 100, stato: CommessaFaseStates.Done, giorniDurata: 5);
        ctx.CreaFase(3, parentId: 1, mode: CommessaFaseCompletionMode.Manual,
            progress: 100, stato: CommessaFaseStates.Done, giorniDurata: 5);

        await ctx.Service.RecomputeFaseProgressAsync(2);

        Assert.Equal(100, ctx.Rileggi(1).Progress);
        Assert.Equal(CommessaFaseStates.Done, ctx.Rileggi(1).State);
        Assert.Equal(CommessaStates.Completed, ctx.RileggiCommessa().State);
    }
}
