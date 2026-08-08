using CRM.Server.Extensions;
using CRM.Server.Services;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Schedulazione all'indietro dal template e propagazione delle date.
/// La consegna è sempre nel futuro e le attese sono espresse con gli stessi helper di calendario:
/// così i test non invecchiano e non dipendono da dove cadono i festivi.
/// </summary>
public class SchedulingTests
{
    /// <summary>
    /// Abbastanza avanti da non far scattare il fallback "consegna già passata"
    /// (verificato separatamente in <see cref="Con_la_consegna_gia_passata_si_riparte_da_oggi"/>).
    /// </summary>
    private static DateTime Consegna => DateTime.Today.AddDays(90).NextWorkday();

    private static GanttPhase Phase(int id, string name, int durata, int sortOrder, bool milestone = false)
        => new()
        {
            Id = id,
            Name = name,
            DurationDays = durata,
            SortOrder = sortOrder,
            IsMilestone = milestone
        };

    /// <summary>
    /// A(6gg) -> B(2gg), Finish-to-Start senza lag. La durata di A è 6 giorni lavorativi di
    /// proposito: una fase così lunga attraversa un weekend qualunque sia il giorno di partenza,
    /// quindi il test distingue davvero i giorni lavorativi da quelli solari. Con durate brevi
    /// l'esito dipenderebbe da dove cade l'inizio, e il test passerebbe a caso.
    /// </summary>
    private const int DurataA = 6;

    private static List<GanttPhase> TemplateAB()
    {
        var a = Phase(10, "A", DurataA, 1);
        var b = Phase(20, "B", 2, 2);
        b.Dependencies.Add(new GanttPhaseDependency { IdPhase = b.Id, IdPredecessorPhase = a.Id, LagDays = 0 });
        return new List<GanttPhase> { a, b };
    }

    [Fact]
    public void Le_fasi_durano_in_giorni_lavorativi()
    {
        var (_, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);

        var a = phases.Single(p => p.Template.Name == "A").Fase;

        // La fase copre 6 giorni feriali, non 6 giorni di calendario: a giorni solari il conteggio
        // dei feriali compresi scenderebbe sotto 6, perché il weekend se ne mangia una parte.
        Assert.Equal(DurataA, a.StartDate.CountWorkdays(a.EndDate));
    }

    [Fact]
    public void Nessuna_fase_inizia_o_finisce_nel_weekend()
    {
        var (start, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);

        Assert.DoesNotContain(start.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        foreach (var (_, fase) in phases)
        {
            Assert.DoesNotContain(fase.StartDate.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
            Assert.DoesNotContain(fase.EndDate.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        }
    }

    [Fact]
    public void Il_successore_inizia_il_giorno_lavorativo_dopo_la_fine_del_predecessore()
    {
        var (_, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);

        var a = phases.Single(p => p.Template.Name == "A").Fase;
        var b = phases.Single(p => p.Template.Name == "B").Fase;

        Assert.Equal(a.EndDate.AddWorkdays(1), b.StartDate);
    }

    [Fact]
    public void La_commessa_finisce_entro_la_consegna()
    {
        var consegna = Consegna;
        var (start, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), consegna);

        Assert.True(start <= consegna);
        Assert.All(phases, p => Assert.True(p.Fase.EndDate <= consegna));
    }

    /// <summary>
    /// Commessa già in ritardo alla nascita: non si pianifica nel passato, si riparte da oggi.
    /// La consegna resta quella richiesta, e sarà il ritardo a essere visibile.
    /// </summary>
    [Fact]
    public void Con_la_consegna_gia_passata_si_riparte_da_oggi()
    {
        var (start, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), DateTime.Today.AddDays(-30));

        Assert.True(start >= DateTime.Today);
        Assert.All(phases, p => Assert.True(p.Fase.StartDate >= DateTime.Today));
    }

    /// <summary>
    /// L'invariante piu' importante del modulo: se le due matematiche non concordassero, ogni
    /// salvataggio di una commessa appena creata sposterebbe le date senza che nessuno le abbia
    /// toccate. E' il motivo per cui questo test esiste.
    /// </summary>
    [Fact]
    public void Un_piano_appena_generato_non_viene_spostato_dalla_propagazione()
    {
        var template = TemplateAB();
        var (_, phases) = CommesseService.BuildPhasesBackward(template, Consegna);

        var (fasi, deps) = Materializza(phases);
        var dateOriginali = fasi.Select(f => (f.StartDate, f.EndDate)).ToList();

        // Senza questa il test passerebbe a vuoto: nessuna dipendenza, nessuna propagazione.
        Assert.NotEmpty(deps);

        var modificato = CommessaFasiService.CascadeDates(fasi, deps, fasi.Select(f => f.Id).ToList());

        Assert.False(modificato);
        Assert.Equal(dateOriginali, fasi.Select(f => (f.StartDate, f.EndDate)).ToList());
    }

    [Fact]
    public void Se_il_predecessore_slitta_il_successore_lo_segue()
    {
        var (_, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);
        var (fasi, deps) = Materializza(phases);

        var a = fasi.Single(f => f.Name == "A");
        var b = fasi.Single(f => f.Name == "B");
        var durataB = b.StartDate.CountWorkdays(b.EndDate);

        // A slitta di 5 giorni lavorativi.
        a.EndDate = a.EndDate.AddWorkdays(5);

        var modificato = CommessaFasiService.CascadeDates(fasi, deps, new List<int> { a.Id });

        Assert.True(modificato);
        Assert.Equal(a.EndDate.AddWorkdays(1), b.StartDate);
        Assert.Equal(durataB, b.StartDate.CountWorkdays(b.EndDate));   // durata conservata
    }

    [Fact]
    public void La_propagazione_non_anticipa_mai_il_lavoro()
    {
        var (_, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);
        var (fasi, deps) = Materializza(phases);

        var a = fasi.Single(f => f.Name == "A");
        var b = fasi.Single(f => f.Name == "B");
        var inizioB = b.StartDate;

        Assert.NotEmpty(deps);

        // A finisce molto prima del previsto: B potrebbe partire subito, ma non lo si tocca.
        a.EndDate = a.EndDate.SubtractWorkdays(10);

        var modificato = CommessaFasiService.CascadeDates(fasi, deps, new List<int> { a.Id });

        Assert.False(modificato);
        Assert.Equal(inizioB, b.StartDate);
    }

    [Fact]
    public void La_propagazione_percorre_la_catena_intera()
    {
        // A -> B -> C
        var a = Phase(10, "A", 2, 1);
        var b = Phase(20, "B", 2, 2);
        var c = Phase(30, "C", 2, 3);
        b.Dependencies.Add(new GanttPhaseDependency { IdPhase = b.Id, IdPredecessorPhase = a.Id });
        c.Dependencies.Add(new GanttPhaseDependency { IdPhase = c.Id, IdPredecessorPhase = b.Id });

        var (_, phases) = CommesseService.BuildPhasesBackward(new List<GanttPhase> { a, b, c }, Consegna);
        var (fasi, deps) = Materializza(phases);

        var fa = fasi.Single(f => f.Name == "A");
        var fc = fasi.Single(f => f.Name == "C");
        var inizioC = fc.StartDate;

        fa.EndDate = fa.EndDate.AddWorkdays(3);
        CommessaFasiService.CascadeDates(fasi, deps, new List<int> { fa.Id });

        Assert.True(fc.StartDate > inizioC);
        Assert.Equal(fasi.Single(f => f.Name == "B").EndDate.AddWorkdays(1), fc.StartDate);
    }

    [Fact]
    public void Il_lag_ritarda_il_successore()
    {
        var a = Phase(10, "A", 2, 1);
        var b = Phase(20, "B", 2, 2);
        b.Dependencies.Add(new GanttPhaseDependency { IdPhase = b.Id, IdPredecessorPhase = a.Id, LagDays = 3 });

        var (_, phases) = CommesseService.BuildPhasesBackward(new List<GanttPhase> { a, b }, Consegna);
        var (fasi, deps) = Materializza(phases);

        var fa = fasi.Single(f => f.Name == "A");
        var fb = fasi.Single(f => f.Name == "B");

        fb.StartDate = fa.EndDate.AddWorkdays(1);   // troppo presto: ignora il lag
        fb.EndDate = fb.StartDate.AddWorkdays(1);

        CommessaFasiService.CascadeDates(fasi, deps, new List<int> { fa.Id });

        Assert.Equal(fa.EndDate.AddWorkdays(4), fb.StartDate);   // 1 giorno + 3 di lag
    }

    // ─── Spostamento del piano su una nuova consegna ─────────────────────────

    /// <summary>
    /// E' l'invariante su cui poggia la traslazione: lo scarto calcolato fra due date, riapplicato,
    /// deve riportare esattamente sulla seconda. Se qui si sfasa di un giorno, si sfasa tutto il piano.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(23)]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-23)]
    public void Lo_scarto_in_giorni_lavorativi_e_reversibile(int giorni)
    {
        var da = Consegna;
        var a = da.ShiftWorkdays(giorni);

        var delta = da.WorkdayDelta(a);

        Assert.Equal(giorni, delta);
        Assert.Equal(a, da.ShiftWorkdays(delta));
    }

    [Fact]
    public void Traslare_il_piano_conserva_durate_e_distanze_fra_le_fasi()
    {
        var (_, phases) = CommesseService.BuildPhasesBackward(TemplateAB(), Consegna);
        var a = phases.Single(p => p.Template.Name == "A").Fase;
        var b = phases.Single(p => p.Template.Name == "B").Fase;

        var durataA = a.StartDate.CountWorkdays(a.EndDate);
        var distanzaAB = a.EndDate.WorkdayDelta(b.StartDate);

        // stesso delta su tutte le fasi: e' quello che fa RescheduleAsync
        const int delta = 10;
        foreach (var (_, f) in phases)
        {
            f.StartDate = f.StartDate.ShiftWorkdays(delta);
            f.EndDate = f.EndDate.ShiftWorkdays(delta);
        }

        Assert.Equal(durataA, a.StartDate.CountWorkdays(a.EndDate));
        Assert.Equal(distanzaAB, a.EndDate.WorkdayDelta(b.StartDate));
    }

    /// <summary>
    /// Una consegna nel weekend arrotonda all'indietro: spostare in avanti al lunedi' regalerebbe
    /// due giorni di produzione che il cliente non ha concesso.
    /// </summary>
    [Fact]
    public void Una_consegna_nel_weekend_arrotonda_al_venerdi()
    {
        var sabato = DateTime.Today.AddDays(60);
        while (sabato.DayOfWeek != DayOfWeek.Saturday)
            sabato = sabato.AddDays(1);

        var consegna = sabato.PreviousWorkday();

        Assert.True(consegna < sabato);
        Assert.Equal(consegna, consegna.PreviousWorkday()); // idempotente su un giorno lavorativo
    }

    // ─── Commessa aperta: fase unica, nessun template ────────────────────────

    private static DateTime ProssimoSabato(DateTime da)
    {
        var d = da.Date;
        while (d.DayOfWeek != DayOfWeek.Saturday)
            d = d.AddDays(1);
        return d;
    }

    /// <summary>
    /// Senza ciclo di produzione non c'è nulla da schedulare, ma il calendario resta quello:
    /// la fase unica deve stare sui giorni lavorativi come le fasi generate dal template.
    /// </summary>
    [Fact]
    public void La_fase_unica_di_una_commessa_aperta_sta_nei_giorni_lavorativi()
    {
        var sabato = ProssimoSabato(DateTime.Today.AddDays(60));

        var (start, end) = CommesseService.OpenPlanWindow(sabato, DateTime.Today);

        Assert.True(end < sabato);   // consegna nel weekend: si chiude entro il venerdì
        Assert.DoesNotContain(start.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        Assert.DoesNotContain(end.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
        Assert.True(start <= end);
    }

    /// <summary>
    /// I festivi non sono solo il weekend: una consegna a Natale va indietro come un sabato.
    /// </summary>
    [Fact]
    public void Una_consegna_su_un_festivo_arretra_al_giorno_lavorativo_precedente()
    {
        var natale = new DateTime(DateTime.Today.Year + 1, 12, 25);

        var (_, end) = CommesseService.OpenPlanWindow(natale, DateTime.Today);

        Assert.True(end < natale);
    }

    /// <summary>
    /// L'apertura avviene di sabato (turno, straordinario, recupero): il piano non può partire
    /// da quel giorno, che sul Gantt non esiste nemmeno come colonna.
    /// </summary>
    [Fact]
    public void Aprendo_la_commessa_in_un_giorno_non_lavorativo_il_piano_parte_dal_primo_feriale()
    {
        var oggi = ProssimoSabato(DateTime.Today.AddDays(7));

        var (start, _) = CommesseService.OpenPlanWindow(oggi.AddDays(30), oggi);

        Assert.True(start > oggi);
        Assert.DoesNotContain(start.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday });
    }

    /// <summary>
    /// Consegna già passata: caso legittimo (commessa aperta su lavoro iniziato prima). Il piano
    /// deve collassare sulla consegna, non iniziare dopo la propria fine.
    /// </summary>
    [Fact]
    public void Con_la_consegna_gia_passata_la_fase_unica_non_inizia_dopo_la_fine()
    {
        var (start, end) = CommesseService.OpenPlanWindow(DateTime.Today.AddDays(-30), DateTime.Today);

        Assert.True(start <= end);
        Assert.True(end < DateTime.Today);
    }

    /// <summary>
    /// Su date già lavorative la finestra non si muove: l'utente ritrova quello che ha scritto.
    /// </summary>
    [Fact]
    public void Su_date_gia_lavorative_la_finestra_resta_quella_richiesta()
    {
        var oggi = DateTime.Today.NextWorkday();
        var consegna = oggi.AddWorkdays(20);

        var (start, end) = CommesseService.OpenPlanWindow(consegna, oggi);

        Assert.Equal(oggi, start);
        Assert.Equal(consegna, end);
    }

    /// <summary>
    /// Assegna gli Id che in produzione arriverebbero da EF dopo il primo SaveChanges e traduce
    /// le dipendenze del template in dipendenze fra fasi.
    /// </summary>
    private static (List<CommessaFase> fasi, List<(int IdFase, int IdPredecessorFase, int LagDays)> deps)
        Materializza(List<(GanttPhase Template, CommessaFase Fase)> phases)
    {
        int nextId = 1;
        foreach (var (_, fase) in phases)
            fase.Id = nextId++;

        var faseByTemplateId = phases.ToDictionary(p => p.Template.Id, p => p.Fase);

        var deps = new List<(int, int, int)>();
        foreach (var (template, fase) in phases)
            foreach (var d in template.Dependencies)
                if (faseByTemplateId.TryGetValue(d.IdPredecessorPhase, out var pred))
                    deps.Add((fase.Id, pred.Id, d.LagDays));

        return (phases.Select(p => p.Fase).ToList(), deps);
    }
}
