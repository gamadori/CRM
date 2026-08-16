using CRM.Server.Services;

namespace CRM.Tests;

/// <summary>
/// A che ora parte il preavviso di scadenza.
/// <para>
/// Il difetto presidiato, misurato sui ticket veri in archivio: con un anticipo di due ore, i
/// ticket di produzione (scadenza a mezzanotte, perche' le fasi ragionano a giornate) avvisavano
/// alle 22:00 della sera prima, e i ticket di assistenza alle 20:30 - l'ora in cui qualcuno aveva
/// premuto "nuovo ticket" due giorni prima. Nessuna delle due l'aveva scelta qualcuno, e in nessuna
/// schermata del programma quell'ora si vedeva.
/// </para>
/// </summary>
public class TicketExpiryScheduleTests
{
    private static readonly DateTime Venerdi = new(2026, 3, 20);

    [Fact]
    public void Senza_orario_di_lavoro_la_giornata_finisce_alle_18()
    {
        Assert.Equal(TimeSpan.FromHours(18), TicketExpirySchedule.FineGiornata(null));
    }

    /// <summary>
    /// In archivio l'orario non configurato non e' vuoto: e' 00:00. Preso alla lettera riporterebbe
    /// il preavviso alla sera prima, cioe' al difetto di partenza.
    /// </summary>
    [Fact]
    public void Un_orario_di_chiusura_a_mezzanotte_vale_come_non_configurato()
    {
        Assert.Equal(TimeSpan.FromHours(18), TicketExpirySchedule.FineGiornata(TimeOnly.MinValue));
    }

    [Fact]
    public void L_orario_di_lavoro_configurato_vince()
    {
        Assert.Equal(TimeSpan.FromHours(17), TicketExpirySchedule.FineGiornata(new TimeOnly(17, 0)));
    }

    /// <summary>Ticket di produzione: la scadenza e' a mezzanotte, e prima avvisava alle 22:00 del giorno prima.</summary>
    [Fact]
    public void Una_scadenza_a_mezzanotte_avvisa_nel_pomeriggio_del_giorno_di_scadenza()
    {
        var preavviso = TicketExpirySchedule.PreavvisoAt(Venerdi, TimeSpan.FromHours(18), 120);

        Assert.Equal(Venerdi.AddHours(16), preavviso);
    }

    /// <summary>Ticket di assistenza: l'ora di creazione appiccicata alla scadenza non conta piu'.</summary>
    [Fact]
    public void L_ora_dentro_la_scadenza_non_sposta_il_preavviso()
    {
        var conOrarioAssurdo = Venerdi.AddHours(22).AddMinutes(30);

        var preavviso = TicketExpirySchedule.PreavvisoAt(conOrarioAssurdo, TimeSpan.FromHours(18), 120);

        Assert.Equal(Venerdi.AddHours(16), preavviso);
    }

    /// <summary>
    /// Chi vuole l'avviso il giorno prima non chiede una modifica al codice: mette 1440 minuti
    /// nell'impostazione e lo riceve a fine giornata del giorno precedente.
    /// </summary>
    [Fact]
    public void Un_anticipo_di_un_giorno_avvisa_a_fine_giornata_precedente()
    {
        var preavviso = TicketExpirySchedule.PreavvisoAt(Venerdi, TimeSpan.FromHours(18), 1440);

        Assert.Equal(Venerdi.AddDays(-1).AddHours(18), preavviso);
    }

    /// <summary>
    /// Un ticket non e' in ritardo la mattina del giorno in cui scade: ha tempo fino a sera. Il
    /// confronto con la fine giornata e' cio' che decide se l'avviso dice "in scadenza" o "scaduto".
    /// </summary>
    [Fact]
    public void Il_giorno_della_scadenza_il_ticket_non_e_ancora_in_ritardo()
    {
        var scadenzaAt = TicketExpirySchedule.ScadenzaAt(Venerdi, TimeSpan.FromHours(18));

        Assert.True(scadenzaAt > Venerdi.AddHours(9));
        Assert.True(scadenzaAt < Venerdi.AddHours(23));
    }
}
