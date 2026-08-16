using CRM.Shared;

namespace CRM.Server.Services
{
    /// <summary>
    /// A che ora scade un ticket, e quindi quando parte il suo preavviso.
    /// <para>
    /// La scadenza di un ticket e' un <b>giorno</b>, non un istante: l'elenco la mostra come data,
    /// la scheda pure, e lo stato "scaduto" confronta i giorni. L'ora finita dentro
    /// <see cref="Ticket.DateExpired"/> non l'ha scelta nessuno - sull'assistenza e' l'ora in cui
    /// qualcuno ha premuto "nuovo ticket" (chi apre alle 22:30 se le porta dietro per sempre), sui
    /// ticket di produzione e' la mezzanotte, perche' le fasi di commessa ragionano a giornate.
    /// </para>
    /// <para>
    /// Il preavviso pero' quell'ora la leggeva, ed e' l'unico posto in tutto il programma dove
    /// contava: un anticipo di due ore su una scadenza a mezzanotte suonava alle 22:00 del giorno
    /// prima, e su una scadenza alle 22:30 suonava alle 20:30. Avvisi di sera, a ufficio chiuso,
    /// per un orario che nessuno aveva stabilito.
    /// </para>
    /// <para>
    /// Qui l'ora viene buttata via e ricostruita: si scade a <b>fine giornata lavorativa</b>, e i
    /// minuti di anticipo si contano da li'. Chi vuole essere avvisato il giorno prima mette 1440
    /// minuti nell'impostazione, senza che nessuno debba toccare il codice.
    /// </para>
    /// </summary>
    public static class TicketExpirySchedule
    {
        /// <summary>
        /// Fine giornata quando l'orario di lavoro non e' configurato. Serve un ripiego vero:
        /// l'impostazione non e' solo annullabile, in archivio si presenta anche come 00:00 - e
        /// prendere quel valore alla lettera riporterebbe il preavviso alle 22:00 della sera prima,
        /// cioe' esattamente al difetto che questa classe risolve.
        /// </summary>
        public static readonly TimeSpan FineGiornataPredefinita = TimeSpan.FromHours(18);

        /// <summary>L'ora a cui si considera finita la giornata, dall'orario di lavoro aziendale.</summary>
        public static TimeSpan FineGiornata(TimeOnly? orarioDiChiusura)
            => orarioDiChiusura is { } chiusura && chiusura != TimeOnly.MinValue
                ? chiusura.ToTimeSpan()
                : FineGiornataPredefinita;

        /// <summary>L'istante in cui il ticket e' davvero scaduto: il suo giorno, a fine giornata.</summary>
        public static DateTime ScadenzaAt(DateTime dateExpired, TimeSpan fineGiornata)
            => dateExpired.Date + fineGiornata;

        /// <summary>Quando far partire il preavviso: tanti minuti prima della fine di quel giorno.</summary>
        public static DateTime PreavvisoAt(DateTime dateExpired, TimeSpan fineGiornata, int minutiDiAnticipo)
            => ScadenzaAt(dateExpired, fineGiornata) - TimeSpan.FromMinutes(Math.Max(0, minutiDiAnticipo));
    }
}
