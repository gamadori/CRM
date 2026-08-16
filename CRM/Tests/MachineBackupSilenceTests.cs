using CRM.Server.Services;

namespace CRM.Tests;

/// <summary>
/// Quali macchine finiscono nel riepilogo di chi ha smesso di mandare il backup.
/// <para>
/// Sui dati veri le nove macchine con backup li hanno caricati tutti a fine luglio, a mano: con una
/// soglia di 30 giorni oggi non si segnalerebbe nessuno, con 15 si segnalerebbero tutte. La soglia
/// e' un'impostazione proprio perche' la risposta giusta dipende da come si lavora.
/// </para>
/// </summary>
public class MachineBackupSilenceTests
{
    private static readonly DateTime Oggi = new(2026, 8, 16);

    private static MacchinaInSilenzio Macchina(string matricola, int giorniFa)
        => new()
        {
            IdArticle = matricola.GetHashCode(),
            SerialNumber = matricola,
            ProductName = "Tornio",
            CompanyName = "Cliente",
            UltimoBackup = Oggi.AddDays(-giorniFa)
        };

    [Fact]
    public void Con_la_sorveglianza_spenta_non_si_segnala_nessuno()
    {
        var macchine = new[] { Macchina("2884", 400) };

        Assert.Empty(MachineBackupSilenceRules.InSilenzio(macchine, Oggi, 0));
    }

    [Fact]
    public void Si_segnala_chi_ha_superato_la_soglia()
    {
        var macchine = new[] { Macchina("2884", 45), Macchina("2902", 10) };

        var risultato = MachineBackupSilenceRules.InSilenzio(macchine, Oggi, 30);

        Assert.Equal(new[] { "2884" }, risultato.Select(x => x.SerialNumber).ToArray());
        Assert.Equal(45, risultato[0].GiorniDiSilenzio);
    }

    /// <summary>
    /// Il giorno esatto della soglia conta come superato: aspettare l'indomani vorrebbe dire che
    /// "avvisami dopo 30 giorni" avvisa dopo 31.
    /// </summary>
    [Fact]
    public void Il_giorno_della_soglia_e_gia_silenzio()
    {
        var risultato = MachineBackupSilenceRules.InSilenzio(new[] { Macchina("2884", 30) }, Oggi, 30);

        Assert.Single(risultato);
    }

    [Fact]
    public void Le_piu_silenziose_stanno_in_cima()
    {
        var macchine = new[] { Macchina("A", 31), Macchina("B", 200), Macchina("C", 60) };

        var risultato = MachineBackupSilenceRules.InSilenzio(macchine, Oggi, 30);

        Assert.Equal(new[] { "B", "C", "A" }, risultato.Select(x => x.SerialNumber).ToArray());
    }

    [Fact]
    public void Senza_destinatari_non_si_manda_niente()
    {
        Assert.Empty(MachineBackupSilenceRules.Destinatari(null));
        Assert.Empty(MachineBackupSilenceRules.Destinatari("   "));
        Assert.Empty(MachineBackupSilenceRules.Destinatari(";;"));
    }

    /// <summary>
    /// Gli indirizzi si separano con il punto e virgola, ma chi li scrive usa anche la virgola, e
    /// mette spazi. Un elenco scritto a mano non deve diventare un indirizzo sbagliato.
    /// </summary>
    [Fact]
    public void Piu_destinatari_si_separano_con_punto_e_virgola_o_virgola()
    {
        var destinatari = MachineBackupSilenceRules.Destinatari(" mario@x.it ; anna@y.it, luca@z.it ");

        Assert.Equal(new[] { "mario@x.it", "anna@y.it", "luca@z.it" }, destinatari.ToArray());
    }

    /// <summary>Lo stesso indirizzo scritto due volte non riceve due email uguali.</summary>
    [Fact]
    public void Un_indirizzo_ripetuto_conta_una_volta_sola()
    {
        var destinatari = MachineBackupSilenceRules.Destinatari("mario@x.it;MARIO@x.it");

        Assert.Single(destinatari);
    }

    /// <summary>
    /// Il riepilogo dice quante sono e da quando tacciono: serve a decidere se e' un caso isolato
    /// o un guasto di rete che ne ha zittite venti insieme.
    /// </summary>
    [Fact]
    public void Il_riepilogo_riporta_matricola_e_giorni()
    {
        var macchine = MachineBackupSilenceRules.InSilenzio(new[] { Macchina("2884", 45) }, Oggi, 30);

        var testo = MachineBackupSilenceRules.Riepilogo(macchine, 30);

        Assert.Contains("2884", testo);
        Assert.Contains("45", testo);
        Assert.Contains("30", testo);
    }
}
