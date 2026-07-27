using CRM.Server.Services;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Stato e avanzamento di una fase: derivano dai ticket o dalla percentuale manuale, mai dal client.
/// </summary>
public class PhaseProgressTests
{
    private static Ticket Ticket(int id, bool chiuso, string? assegnato = null, string aperto = "u1")
        => new()
        {
            Id = id,
            Closed = chiuso,
            DateOpened = new DateTime(2026, 3, 2).AddDays(id),
            IdUserOpened = aperto,
            IdUserAssigned = assegnato,
            Description = string.Empty,
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        };

    private static CommessaFase Fase(
        CommessaFaseCompletionMode mode = CommessaFaseCompletionMode.AllTicketsClosed,
        int progress = 0)
        => new()
        {
            Id = 1,
            Name = "Fase",
            CompletionMode = mode,
            Progress = progress,
            StartDate = new DateTime(2026, 3, 2),
            EndDate = new DateTime(2026, 3, 6)
        };

    // ─── Avanzamento guidato dai ticket ──────────────────────────────────────

    [Fact]
    public void Con_tutti_i_ticket_chiusi_la_fase_e_conclusa()
    {
        var f = Fase();
        f.Tickets.Add(Ticket(1, chiuso: true));
        f.Tickets.Add(Ticket(2, chiuso: true));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(100, f.Progress);
        Assert.Equal(CommessaFaseStates.Done, f.State);
    }

    [Fact]
    public void Con_un_ticket_aperto_la_fase_e_in_lavorazione()
    {
        var f = Fase();
        f.Tickets.Add(Ticket(1, chiuso: true));
        f.Tickets.Add(Ticket(2, chiuso: false));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(50, f.Progress);
        Assert.Equal(CommessaFaseStates.InProgress, f.State);
    }

    /// <summary>
    /// Regressione: il ricalcolo non caricava i piani ticket, quindi contava solo i ticket
    /// esistenti. Con 3 previsti obbligatori e un solo ticket chiuso la fase risultava conclusa.
    /// </summary>
    [Fact]
    public void I_previsti_obbligatori_non_ancora_generati_contano_nel_denominatore()
    {
        var f = Fase();
        var chiuso = Ticket(1, chiuso: true);
        f.Tickets.Add(chiuso);
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 1, Required = true, IdTicket = chiuso.Id, Ticket = chiuso });
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 2, Required = true });
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 3, Required = true });

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(33, f.Progress);
        Assert.NotEqual(CommessaFaseStates.Done, f.State);
    }

    /// <summary>
    /// Regressione: i soli piani previsti facevano risultare "avviata" una fase su cui nessuno
    /// aveva ancora aperto un ticket. Rinominarla dal Gantt la portava a InProgress.
    /// </summary>
    [Fact]
    public void I_previsti_senza_ticket_generati_lasciano_la_fase_da_iniziare()
    {
        var f = Fase();
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 1, Required = true });
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 2, Required = true });

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(0, f.Progress);
        Assert.Equal(CommessaFaseStates.Pending, f.State);
        Assert.Null(f.TakenAt);
    }

    [Fact]
    public void Con_la_regola_un_ticket_chiuso_basta_il_primo()
    {
        var f = Fase(CommessaFaseCompletionMode.AnyTicketClosed);
        f.Tickets.Add(Ticket(1, chiuso: true));
        f.Tickets.Add(Ticket(2, chiuso: false));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(100, f.Progress);
        Assert.Equal(CommessaFaseStates.Done, f.State);
    }

    [Fact]
    public void I_previsti_facoltativi_non_pesano()
    {
        var f = Fase();
        var chiuso = Ticket(1, chiuso: true);
        f.Tickets.Add(chiuso);
        f.TicketPlans.Add(new CommessaFaseTicketPlan { Id = 1, Required = false });

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(100, f.Progress);
    }

    // ─── Avanzamento manuale ─────────────────────────────────────────────────

    [Fact]
    public void In_modalita_manuale_comanda_la_percentuale()
    {
        var f = Fase(CommessaFaseCompletionMode.Manual, progress: 100);
        CommessaFasiService.ApplyStateAndProgress(f);
        Assert.Equal(CommessaFaseStates.Done, f.State);

        var g = Fase(CommessaFaseCompletionMode.Manual, progress: 40);
        CommessaFasiService.ApplyStateAndProgress(g);
        Assert.Equal(CommessaFaseStates.InProgress, g.State);

        var h = Fase(CommessaFaseCompletionMode.Manual, progress: 0);
        CommessaFasiService.ApplyStateAndProgress(h);
        Assert.Equal(CommessaFaseStates.Pending, h.State);
    }

    [Fact]
    public void In_modalita_manuale_i_ticket_non_toccano_la_percentuale()
    {
        var f = Fase(CommessaFaseCompletionMode.ProgressManual, progress: 20);
        f.Tickets.Add(Ticket(1, chiuso: true));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(20, f.Progress);
    }

    [Fact]
    public void Senza_alcun_ticket_la_fase_resta_completabile_a_mano()
    {
        // Fase amministrativa: nessun ticket da cui derivare, vale la percentuale.
        var f = Fase(CommessaFaseCompletionMode.AllTicketsClosed, progress: 100);

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal(CommessaFaseStates.Done, f.State);
    }

    // ─── Presa in carico ─────────────────────────────────────────────────────

    [Fact]
    public void La_presa_in_carico_registra_utente_e_data_dal_primo_ticket()
    {
        var f = Fase();
        f.Tickets.Add(Ticket(2, chiuso: false, assegnato: "mario"));
        f.Tickets.Add(Ticket(1, chiuso: false, assegnato: "luigi"));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal("luigi", f.IdUserTakenBy);   // il ticket aperto per primo
        Assert.NotNull(f.TakenAt);
    }

    [Fact]
    public void Senza_assegnatario_vale_chi_ha_aperto_il_ticket()
    {
        var f = Fase();
        f.Tickets.Add(Ticket(1, chiuso: false, assegnato: null, aperto: "anna"));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal("anna", f.IdUserTakenBy);
    }

    [Fact]
    public void La_presa_in_carico_non_viene_riscritta()
    {
        var f = Fase();
        f.IdUserTakenBy = "primo";
        f.TakenAt = new DateTime(2026, 1, 1);
        f.Tickets.Add(Ticket(1, chiuso: false, assegnato: "secondo"));

        CommessaFasiService.ApplyStateAndProgress(f);

        Assert.Equal("primo", f.IdUserTakenBy);
        Assert.Equal(new DateTime(2026, 1, 1), f.TakenAt);
    }

    // ─── Raggruppamenti e media di commessa ──────────────────────────────────

    [Fact]
    public void Il_raggruppamento_e_concluso_solo_con_tutti_i_figli_conclusi()
    {
        Assert.Equal(CommessaFaseStates.Done, CommessaFasiService.RollupState(
            new[] { CommessaFaseStates.Done, CommessaFaseStates.Done }));

        Assert.Equal(CommessaFaseStates.InProgress, CommessaFasiService.RollupState(
            new[] { CommessaFaseStates.Done, CommessaFaseStates.Pending }));

        Assert.Equal(CommessaFaseStates.InProgress, CommessaFasiService.RollupState(
            new[] { CommessaFaseStates.InProgress, CommessaFaseStates.Pending }));

        Assert.Equal(CommessaFaseStates.Pending, CommessaFasiService.RollupState(
            new[] { CommessaFaseStates.Pending, CommessaFaseStates.Pending }));
    }

    [Fact]
    public void La_media_pesa_le_fasi_sulla_durata()
    {
        // 10 giorni al 100% e 1 giorno allo 0%: la media non è 50%.
        var items = new List<(DateTime, DateTime, int, bool)>
        {
            (new DateTime(2026, 3, 2), new DateTime(2026, 3, 11), 100, false),  // 10 giorni
            (new DateTime(2026, 3, 12), new DateTime(2026, 3, 12), 0, false)    // 1 giorno
        };

        Assert.Equal(91, CommessaFasiService.WeightedProgress(items));
    }

    [Fact]
    public void Le_milestone_non_entrano_nella_media()
    {
        var items = new List<(DateTime, DateTime, int, bool)>
        {
            (new DateTime(2026, 3, 2), new DateTime(2026, 3, 6), 100, false),
            (new DateTime(2026, 3, 6), new DateTime(2026, 3, 6), 0, true)   // milestone aperta
        };

        Assert.Equal(100, CommessaFasiService.WeightedProgress(items));
    }

    [Fact]
    public void Senza_fasi_la_media_e_zero()
    {
        Assert.Equal(0, CommessaFasiService.WeightedProgress(new List<(DateTime, DateTime, int, bool)>()));
    }
}
