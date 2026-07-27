using CRM.Server.Extensions;

namespace CRM.Tests;

/// <summary>
/// Calendario lavorativo. Le date dei test stanno tutte a marzo 2026, che non contiene festivi
/// italiani: cosi' l'unica variabile e' il weekend e i test restano leggibili.
/// Riferimenti: lun 2, ven 6, lun 9, ven 13, lun 16, ven 20 marzo 2026.
/// </summary>
public class WorkdayTests
{
    private static DateTime D(int day) => new(2026, 3, day);

    [Fact]
    public void AddWorkdays_scavalca_il_weekend()
    {
        // Da venerdì 6, un giorno lavorativo dopo è lunedì 9, non sabato 7.
        Assert.Equal(D(9), D(6).AddWorkdays(1));
    }

    [Fact]
    public void AddWorkdays_zero_non_sposta()
    {
        Assert.Equal(D(6), D(6).AddWorkdays(0));
    }

    [Fact]
    public void Una_fase_di_cinque_giorni_da_venerdi_finisce_il_giovedi_dopo()
    {
        // 5 giorni lavorativi = ven 6, lun 9, mar 10, mer 11, gio 12.
        // A giorni solari sarebbe finita martedì 10, ed era il bug.
        Assert.Equal(D(12), D(6).AddWorkdays(5 - 1));
    }

    [Fact]
    public void SubtractWorkdays_torna_indietro_saltando_il_weekend()
    {
        // Da lunedì 9, un giorno lavorativo prima è venerdì 6.
        Assert.Equal(D(6), D(9).SubtractWorkdays(1));
    }

    [Fact]
    public void SubtractWorkdays_e_AddWorkdays_sono_simmetrici()
    {
        var arrivo = D(20);
        var partenza = arrivo.SubtractWorkdays(7);
        Assert.Equal(arrivo, partenza.AddWorkdays(7));
    }

    [Fact]
    public void NextWorkday_sposta_il_sabato_al_lunedi()
    {
        Assert.Equal(D(9), D(7).NextWorkday());   // sabato 7 -> lunedì 9
        Assert.Equal(D(9), D(8).NextWorkday());   // domenica 8 -> lunedì 9
        Assert.Equal(D(6), D(6).NextWorkday());   // venerdì resta venerdì
    }

    [Fact]
    public void CountWorkdays_conta_gli_estremi_ed_esclude_il_weekend()
    {
        Assert.Equal(1, D(6).CountWorkdays(D(6)));      // stesso giorno
        Assert.Equal(2, D(6).CountWorkdays(D(9)));      // ven + lun, sabato e domenica esclusi
        Assert.Equal(5, D(2).CountWorkdays(D(6)));      // lun-ven
        Assert.Equal(0, D(9).CountWorkdays(D(6)));      // intervallo invertito
    }

    [Fact]
    public void I_festivi_italiani_non_sono_lavorativi()
    {
        // Ferragosto 2026 cade di sabato, quindi serve un festivo infrasettimanale:
        // il 25 aprile 2026 è sabato, il 1 maggio 2026 è venerdì.
        var giovedi30Aprile = new DateTime(2026, 4, 30);
        // Il giorno lavorativo dopo giovedì 30 non è venerdì 1 maggio (festa dei lavoratori)
        // ma lunedì 4 maggio.
        Assert.Equal(new DateTime(2026, 5, 4), giovedi30Aprile.AddWorkdays(1));
    }
}
