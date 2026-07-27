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
