using CRM.Server.Extensions;
using CRM.Server.Services;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Il piano segue il lavoro vero. Una fase che sfora spinge in avanti quelle che vengono dopo;
/// una che chiude in anticipo non tira indietro nessuno, e il tempo guadagnato si recupera solo
/// quando qualcuno lo decide. Chi ha gia' cominciato non si tocca mai.
/// </summary>
public class ActualEndShiftTests
{
    private static DateTime Lunedi => ProductionTestContext.ProssimoLunedi.AddDays(7);

    private static CommessaFase Fase(int id, DateTime inizio, int durataGiorniLavorativi,
        CommessaFaseStates stato = CommessaFaseStates.Pending)
        => new()
        {
            Id = id,
            Name = $"Fase {id}",
            StartDate = inizio,
            EndDate = inizio.AddWorkdays(durataGiorniLavorativi - 1),
            State = stato
        };

    private static List<(int IdFase, int IdPredecessorFase, int LagDays)> Catena(params (int, int)[] coppie)
        => coppie.Select(c => (c.Item1, c.Item2, 0)).ToList();

    // ─── Ritardo: le successive slittano ─────────────────────────────────────

    [Fact]
    public void Una_fase_chiusa_in_ritardo_spinge_avanti_la_successiva()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 2);
        var inizioBPrima = b.StartDate;

        // Chiusa tre giorni lavorativi oltre il previsto.
        a.EndDateActual = a.EndDate.AddWorkdays(3);

        var mosso = CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b }, Catena((2, 1)), new List<int> { 1 });

        Assert.True(mosso);
        Assert.Equal(a.EndDateActual.Value.AddWorkdays(1), b.StartDate);
        Assert.True(b.StartDate > inizioBPrima);
    }

    [Fact]
    public void Il_ritardo_si_propaga_lungo_tutta_la_catena()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 2);
        var c = Fase(3, b.EndDate.AddWorkdays(1), 2);

        a.EndDateActual = a.EndDate.AddWorkdays(5);

        CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b, c }, Catena((2, 1), (3, 2)), new List<int> { 1 });

        Assert.Equal(b.EndDate.AddWorkdays(1), c.StartDate);
    }

    /// <summary>La durata di chi slitta non cambia: si sposta la finestra, non si comprime il lavoro.</summary>
    [Fact]
    public void Chi_slitta_conserva_la_propria_durata()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 4);
        var durataB = b.StartDate.CountWorkdays(b.EndDate);

        a.EndDateActual = a.EndDate.AddWorkdays(6);

        CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b }, Catena((2, 1)), new List<int> { 1 });

        Assert.Equal(durataB, b.StartDate.CountWorkdays(b.EndDate));
    }

    // ─── Anticipo: non si muove niente ───────────────────────────────────────

    [Fact]
    public void Una_fase_chiusa_in_anticipo_non_tira_avanti_la_successiva()
    {
        var a = Fase(1, Lunedi, 5);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 2);
        var inizioBPrima = b.StartDate;

        a.EndDateActual = a.EndDate.SubtractWorkdays(3);

        var mosso = CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b }, Catena((2, 1)), new List<int> { 1 });

        Assert.False(mosso);
        Assert.Equal(inizioBPrima, b.StartDate);
    }

    // ─── Fasi gia' avviate ───────────────────────────────────────────────────

    /// <summary>
    /// Spostare una fase avviata vorrebbe dire cambiare la scadenza di un ticket su cui qualcuno
    /// sta lavorando. E' la regola che Riprogramma applica gia': ora la applica anche la propagazione.
    /// </summary>
    [Fact]
    public void Una_fase_successiva_gia_avviata_non_viene_spostata()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 2, CommessaFaseStates.InProgress);
        var inizioBPrima = b.StartDate;

        a.EndDateActual = a.EndDate.AddWorkdays(4);

        var mosso = CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b }, Catena((2, 1)), new List<int> { 1 });

        Assert.False(mosso);
        Assert.Equal(inizioBPrima, b.StartDate);
    }

    /// <summary>Una fase avviata ferma la catena a se': chi viene dopo di lei non si muove.</summary>
    [Fact]
    public void La_catena_si_ferma_sulla_fase_gia_avviata()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 2, CommessaFaseStates.InProgress);
        var c = Fase(3, b.EndDate.AddWorkdays(1), 2);
        var inizioCPrima = c.StartDate;

        a.EndDateActual = a.EndDate.AddWorkdays(4);

        CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b, c }, Catena((2, 1), (3, 2)), new List<int> { 1 });

        Assert.Equal(inizioCPrima, c.StartDate);
    }

    /// <summary>Senza fine effettiva vale la previsione: il comportamento di prima non cambia.</summary>
    [Fact]
    public void Senza_fine_effettiva_si_usa_la_data_pianificata()
    {
        var a = Fase(1, Lunedi, 3);
        var b = Fase(2, Lunedi, 2);   // parte troppo presto: viola il vincolo

        CommessaFasiService.CascadeDates(new List<CommessaFase> { a, b }, Catena((2, 1)), new List<int> { 1 });

        Assert.Equal(a.EndDate.AddWorkdays(1), b.StartDate);
    }

    // ─── Compatta il piano ───────────────────────────────────────────────────

    [Fact]
    public void Compattare_recupera_i_giorni_guadagnati_dalla_fase_conclusa_in_anticipo()
    {
        var a = Fase(1, Lunedi, 5, CommessaFaseStates.Done);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 3);
        a.EndDateActual = a.EndDate.SubtractWorkdays(3);

        var spostate = CommesseService.CompactPlan(new List<CommessaFase> { a, b }, Catena((2, 1)), DateTime.Today);

        Assert.Equal(1, spostate);
        Assert.Equal(a.EndDateActual.Value.AddWorkdays(1), b.StartDate);
        Assert.Equal(3, b.StartDate.CountWorkdays(b.EndDate));   // durata conservata
    }

    /// <summary>
    /// Una fase senza predecessori sta dove sta per un motivo che il piano non conosce: materiale
    /// ordinato, reparto impegnato. Compattare recupera l'anticipo maturato, non ripianifica.
    /// </summary>
    [Fact]
    public void Compattare_non_tocca_le_fasi_senza_predecessori()
    {
        var sola = Fase(1, Lunedi.AddWorkdays(20), 3);
        var inizioPrima = sola.StartDate;

        var spostate = CommesseService.CompactPlan(new List<CommessaFase> { sola }, new(), DateTime.Today);

        Assert.Equal(0, spostate);
        Assert.Equal(inizioPrima, sola.StartDate);
    }

    [Fact]
    public void Compattare_non_tocca_le_fasi_gia_avviate()
    {
        var a = Fase(1, Lunedi, 5, CommessaFaseStates.Done);
        var b = Fase(2, a.EndDate.AddWorkdays(5), 3, CommessaFaseStates.InProgress);
        var inizioBPrima = b.StartDate;
        a.EndDateActual = a.EndDate;

        var spostate = CommesseService.CompactPlan(new List<CommessaFase> { a, b }, Catena((2, 1)), DateTime.Today);

        Assert.Equal(0, spostate);
        Assert.Equal(inizioBPrima, b.StartDate);
    }

    /// <summary>Una fase da fare non puo' essere anticipata a ieri.</summary>
    [Fact]
    public void Compattare_non_porta_una_fase_prima_di_oggi()
    {
        var a = Fase(1, DateTime.Today.SubtractWorkdays(20), 3, CommessaFaseStates.Done);
        a.EndDateActual = a.EndDate;
        var b = Fase(2, DateTime.Today.AddWorkdays(10), 3);

        CommesseService.CompactPlan(new List<CommessaFase> { a, b }, Catena((2, 1)), DateTime.Today);

        Assert.True(b.StartDate >= DateTime.Today);
    }

    /// <summary>L'anticipo si propaga lungo la catena in una passata sola.</summary>
    [Fact]
    public void Compattare_propaga_l_anticipo_a_tutta_la_catena()
    {
        var a = Fase(1, Lunedi, 5, CommessaFaseStates.Done);
        var b = Fase(2, a.EndDate.AddWorkdays(1), 3);
        var c = Fase(3, b.EndDate.AddWorkdays(1), 2);
        var inizioCPrima = c.StartDate;
        a.EndDateActual = a.EndDate.SubtractWorkdays(3);

        var spostate = CommesseService.CompactPlan(new List<CommessaFase> { a, b, c }, Catena((2, 1), (3, 2)), DateTime.Today);

        Assert.Equal(2, spostate);
        Assert.True(c.StartDate < inizioCPrima);
        Assert.Equal(b.EndDate.AddWorkdays(1), c.StartDate);
    }

    [Fact]
    public void Su_un_piano_gia_compatto_non_si_sposta_niente()
    {
        var a = Fase(1, Lunedi, 5, CommessaFaseStates.Done);
        a.EndDateActual = a.EndDate;
        var b = Fase(2, a.EndDate.AddWorkdays(1), 3);

        var spostate = CommesseService.CompactPlan(new List<CommessaFase> { a, b }, Catena((2, 1)), DateTime.Today);

        Assert.Equal(0, spostate);
    }
}
