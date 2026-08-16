using CRM.Shared;
using NSubstitute;
using static CRM.Shared.LogEvent;

namespace CRM.Tests;

/// <summary>
/// L'avanzamento di una fase è un valore memorizzato, e un valore memorizzato può essere sbagliato.
/// Qui si prova che la lettura del piano lo rimette in discussione — e che una divergenza non passa
/// mai in silenzio: chi guarda il Gantt vede il numero giusto, chi guarda il registro eventi trova
/// scritto che qualcuno aveva salvato quello sbagliato.
/// </summary>
public class PhaseProgressHealTests
{
    private static ProductionTestContext ConCommessa()
    {
        var ctx = ProductionTestContext.ComeAdmin();
        ctx.CreaCommessa();
        return ctx;
    }

    /// <summary>Il caso reale: fase con l'unico ticket richiesto chiuso, rimasta a 0%.</summary>
    private static ProductionTestContext ConFaseSbagliata()
    {
        var ctx = ConCommessa();
        ctx.CreaFase(1, nome: "Recupero e verifica materiale");
        var piano = ctx.CreaPianoTicket(1, idFase: 1);
        var ticket = ctx.CreaTicket(10, idFase: 1, chiuso: true);

        piano.IdTicket = ticket.Id;

        // Lo stato che si trova in archivio: quello che la fase aveva PRIMA che il ticket
        // venisse chiuso. È la fotografia sbagliata da cui parte il test.
        var fase = ctx.Db.CommessaFasi.Single(f => f.Id == 1);
        fase.Progress = 0;
        fase.State = CommessaFaseStates.InProgress;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        return ctx;
    }

    [Fact]
    public async Task La_lettura_del_piano_corregge_un_avanzamento_smentito_dai_ticket()
    {
        using var ctx = ConFaseSbagliata();

        var piano = await ctx.Service.GetTreeAsync(1);

        var fase = piano!.Single();
        Assert.Equal(100, fase.Progress);
        Assert.Equal(CommessaFaseStates.Done, fase.State);
    }

    /// <summary>
    /// Non basta mostrare il numero giusto: se la correzione restasse in memoria, il database
    /// continuerebbe a raccontare la versione sbagliata a tutti gli altri (agenda, elenchi, riga
    /// d'ordine) e la fase tornerebbe a 0% alla prima scrittura.
    /// </summary>
    [Fact]
    public async Task La_correzione_viene_scritta_sul_database()
    {
        using var ctx = ConFaseSbagliata();

        await ctx.Service.GetTreeAsync(1);

        var fase = ctx.Rileggi(1);
        Assert.Equal(100, fase.Progress);
        Assert.Equal(CommessaFaseStates.Done, fase.State);
    }

    /// <summary>La cascata non si ferma alla fase: la commessa seguiva lo stesso valore sbagliato.</summary>
    [Fact]
    public async Task La_correzione_risale_alla_commessa()
    {
        using var ctx = ConFaseSbagliata();

        await ctx.Service.GetTreeAsync(1);

        var commessa = ctx.RileggiCommessa();
        Assert.Equal(100, commessa.Progress);
        Assert.Equal(CommessaStates.Completed, commessa.State);
    }

    /// <summary>
    /// Il punto per cui questa rete esiste: la divergenza è un difetto a monte, e deve lasciare
    /// una traccia con cui rintracciarlo. Una riparazione silenziosa nasconderebbe il problema
    /// invece di risolverlo.
    /// </summary>
    [Fact]
    public async Task La_divergenza_finisce_nel_registro_eventi()
    {
        using var ctx = ConFaseSbagliata();

        await ctx.Service.GetTreeAsync(1);

        await ctx.Log.Received().RegisterAsync(
            "CommessaFasiService",
            "HealStoredProgressAsync",
            EventsTypes.Warning,
            Arg.Is<string>(m => m.Contains("Avanzamento incoerente")
                                && m.Contains("Recupero e verifica materiale")
                                && m.Contains("ticket 10 chiuso")));
    }

    [Fact]
    public async Task Un_piano_coerente_non_viene_toccato_e_non_segnala_niente()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, stato: CommessaFaseStates.InProgress);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);
        piano.IdTicket = ctx.CreaTicket(10, idFase: 1, chiuso: false).Id;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        var albero = await ctx.Service.GetTreeAsync(1);

        Assert.Equal(0, albero!.Single().Progress);
        Assert.Equal(CommessaFaseStates.InProgress, albero.Single().State);
        await ctx.Log.DidNotReceive().RegisterAsync(
            Arg.Any<string>(), Arg.Any<string>(), EventsTypes.Warning, Arg.Any<string>());
    }

    /// <summary>
    /// Un raggruppamento non ha lavoro proprio: il suo avanzamento è la sintesi dei figli.
    /// Ricalcolarlo dai ticket appesi al padre — che non ce ne sono — lo azzererebbe a ogni lettura.
    /// </summary>
    [Fact]
    public async Task Il_valore_di_un_raggruppamento_non_viene_azzerato_dalla_lettura()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, nome: "Montaggio", progress: 100, stato: CommessaFaseStates.Done,
            mode: CommessaFaseCompletionMode.Manual);
        ctx.CreaFase(2, nome: "Sotto-fase", parentId: 1, progress: 100, stato: CommessaFaseStates.Done,
            mode: CommessaFaseCompletionMode.Manual);
        ctx.Db.ChangeTracker.Clear();

        var albero = await ctx.Service.GetTreeAsync(1);

        var padre = albero!.Single(f => f.Id == 1);
        Assert.Equal(100, padre.Progress);
        Assert.Equal(CommessaFaseStates.Done, padre.State);
    }

    // ─── La trappola sulle scritture ─────────────────────────────────────────

    /// <summary>
    /// Una fase che torna indietro è quasi sempre legittima (ticket riaperto) e ogni tanto è il
    /// difetto che si sta cercando. In entrambi i casi ora resta scritto chi l'ha fatta retrocedere
    /// e su quali dati ha deciso: prima non lo diceva nessuno.
    /// </summary>
    [Fact]
    public async Task Una_fase_che_torna_indietro_viene_registrata_con_i_dati_su_cui_si_e_deciso()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, progress: 100, stato: CommessaFaseStates.Done);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);
        var ticket = ctx.CreaTicket(10, idFase: 1, chiuso: true);
        piano.IdTicket = ticket.Id;
        ctx.Db.SaveChanges();

        // Il ticket viene riaperto: la fase non è più conclusa.
        ticket.Closed = false;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        await ctx.Service.RecomputeFaseProgressAsync(1);

        await ctx.Log.Received().RegisterAsync(
            "CommessaFasiService",
            "RecomputeFaseProgressAsync",
            EventsTypes.Warning,
            Arg.Is<string>(m => m.Contains("tornata indietro")
                                && m.Contains("100% Done -> 0%")
                                && m.Contains("ticket 10 aperto")));
    }

    /// <summary>
    /// Il caso che ha generato tutta questa indagine. "Recupero e verifica materiale" aveva un
    /// figlio (per un annidamento sbagliato nel modello) e insieme un ticket suo, chiuso. Il valore
    /// del raggruppamento è la sintesi dei figli — giusto — e quel ticket chiuso finiva in nulla:
    /// zero nel Gantt, zero nella commessa, che le fasi con figli le scarta. Nessuno lo diceva.
    /// </summary>
    [Fact]
    public async Task Un_raggruppamento_con_lavoro_proprio_lo_dichiara_invece_di_buttarlo_via()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, nome: "Recupero e verifica materiale");
        ctx.CreaFase(2, nome: "Creazione telaio", parentId: 1);

        // Il ticket della fase padre: chiuso, quindi il suo lavoro proprio varrebbe 100%.
        var piano = ctx.CreaPianoTicket(1, idFase: 1);
        piano.IdTicket = ctx.CreaTicket(10, idFase: 1, chiuso: true).Id;

        // Il figlio è ancora fermo: la sintesi dice 0%.
        var pianoFiglio = ctx.CreaPianoTicket(2, idFase: 2);
        pianoFiglio.IdTicket = ctx.CreaTicket(11, idFase: 2, chiuso: false).Id;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        await ctx.Service.RecomputeFaseProgressAsync(1);

        Assert.Equal(0, ctx.Rileggi(1).Progress);   // il rollup comanda, come deve
        await ctx.Log.Received().RegisterAsync(
            "CommessaFasiService",
            "RecomputeRollupFromAsync",
            EventsTypes.Warning,
            Arg.Is<string>(m => m.Contains("sotto-fasi E lavoro proprio")
                                && m.Contains("i suoi ticket direbbero 100%")));
    }

    /// <summary>Un raggruppamento senza lavoro proprio è la normalità: non deve dire niente.</summary>
    [Fact]
    public async Task Un_raggruppamento_senza_ticket_propri_non_segnala_niente()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, nome: "Montaggio");
        ctx.CreaFase(2, nome: "Sotto-fase", parentId: 1);
        var piano = ctx.CreaPianoTicket(2, idFase: 2);
        piano.IdTicket = ctx.CreaTicket(11, idFase: 2, chiuso: true).Id;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        await ctx.Service.RecomputeFaseProgressAsync(2);

        await ctx.Log.DidNotReceive().RegisterAsync(
            Arg.Any<string>(), "RecomputeRollupFromAsync", EventsTypes.Warning, Arg.Any<string>());
    }

    /// <summary>
    /// La distinzione che serviva per risalire a monte: un ticket non caricato dalla query e un
    /// ticket ancora aperto portano allo stesso numero, ma il primo è un difetto e il secondo no.
    /// Nel registro devono leggersi diversi.
    /// </summary>
    [Fact]
    public async Task Un_ticket_non_caricato_si_distingue_da_uno_aperto()
    {
        using var ctx = ConCommessa();
        ctx.CreaFase(1, progress: 100, stato: CommessaFaseStates.Done);
        var piano = ctx.CreaPianoTicket(1, idFase: 1);

        // Piano che punta a un ticket inesistente: la navigazione resta vuota, esattamente come
        // quando una query dimentica di includerla.
        piano.IdTicket = 999;
        ctx.Db.SaveChanges();
        ctx.Db.ChangeTracker.Clear();

        await ctx.Service.RecomputeFaseProgressAsync(1);

        await ctx.Log.Received().RegisterAsync(
            "CommessaFasiService",
            "RecomputeFaseProgressAsync",
            EventsTypes.Warning,
            Arg.Is<string>(m => m.Contains("ticket 999 NON CARICATO")));
    }
}
