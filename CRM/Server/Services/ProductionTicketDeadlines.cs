using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Regola unica sulle date dei ticket di produzione: la fase e' il piano, il ticket ne e'
    /// l'esecuzione.
    /// <para>
    /// La fase detta la <b>scadenza</b> (<see cref="Ticket.DateExpired"/>): date diverse vorrebbero
    /// dire che il Gantt e l'elenco ticket raccontano scadenze diverse, e il calcolo SLA per tipo
    /// ticket (giorni dall'apertura) non conosce la data di consegna.
    /// </para>
    /// <para>
    /// Sull'<b>appuntamento</b> (<see cref="Ticket.Date"/>/<see cref="Ticket.DateEnd"/>) la fase e'
    /// invece un <b>vincolo, non un'assegnazione</b>. Prima la fine del ticket veniva incollata alla
    /// fine della fase: l'agenda, che disegna un blocco continuo da Date a DateEnd, mostrava il
    /// tecnico occupato per tutta la durata della fase - una fase da 30 giorni lo occupava 30 giorni
    /// su 30. Ma la durata pianificata di una fase e' una stima, non un'occupazione: quanto si e'
    /// lavorato davvero lo dicono gli interventi. Quindi qui l'appuntamento viene solo riportato
    /// dentro la finestra se ne esce, e chi pianifica resta libero di collocarlo dove vuole.
    /// </para>
    /// <para>Vale solo sui ticket aperti: su un chiuso le date sono storia.</para>
    /// </summary>
    internal static class ProductionTicketDeadlines
    {
        /// <summary>
        /// Allinea in memoria le date del ticket alla finestra della fase. Restituisce true se ha
        /// cambiato qualcosa: spostare una data rimette in coda il preavviso che la riguarda,
        /// altrimenti quello gia' inviato per la vecchia data varrebbe come inviato anche per la nuova.
        /// </summary>
        public static bool Apply(Ticket ticket, DateTime faseStart, DateTime faseEnd)
        {
            var scadenza = faseEnd.Date;
            var apertura = faseStart.Date;

            // Finestra invertita (fine prima dell'inizio): si degrada a un giorno solo invece di
            // produrre un vincolo che nessuna data puo' soddisfare.
            if (apertura > scadenza)
                apertura = scadenza;

            bool cambiato = false;

            if (ticket.DateExpired?.Date != scadenza)
            {
                ticket.DateExpired = scadenza;
                ticket.ReminderExpiryStatus = ReminderStatus.Pending;
                ticket.ReminderExpiryRetryCount = 0;
                ticket.ReminderExpiryLastAttemptAt = null;
                cambiato = true;
            }

            // Inizio: se il ticket non e' ancora pianificato lo si mette all'inizio della fase, cosi'
            // in agenda compare il giorno in cui il piano dice di cominciare. Se invece qualcuno lo
            // ha gia' collocato, si tocca solo quando e' fuori finestra - e cambia il giorno, non
            // l'ora: chi ha pianificato alle 9:00 se le ritrova alle 9:00 del nuovo giorno.
            var inizio = ticket.Date is { } pianificato
                ? ClampGiorno(pianificato, apertura, scadenza)
                : apertura;

            if (ticket.Date != inizio)
            {
                ticket.Date = inizio;
                ticket.ReminderApptStatus = ReminderStatus.Pending;
                ticket.ReminderApptRetryCount = 0;
                ticket.ReminderApptLastAttemptAt = null;
                cambiato = true;
            }

            // La fine non viene mai creata: un ticket senza fine e' un appuntamento di durata
            // predefinita in agenda, ed e' l'unica lettura onesta finche' nessuno ha deciso quanto
            // durera' il lavoro. Se c'e', va tenuta dentro la finestra e mai prima dell'inizio.
            if (ticket.DateEnd is { } fine)
            {
                var nuovaFine = ClampGiorno(fine, inizio.Date, scadenza);

                if (nuovaFine < inizio)
                    nuovaFine = inizio;

                if (ticket.DateEnd != nuovaFine)
                {
                    ticket.DateEnd = nuovaFine;
                    cambiato = true;
                }
            }

            return cambiato;
        }

        /// <summary>
        /// Riporta il giorno dentro <paramref name="giornoMin"/>..<paramref name="giornoMax"/>
        /// conservando l'ora. Chi e' gia' dentro non viene toccato.
        /// </summary>
        private static DateTime ClampGiorno(DateTime valore, DateTime giornoMin, DateTime giornoMax)
        {
            if (valore.Date < giornoMin)
                return giornoMin.Add(valore.TimeOfDay);

            if (valore.Date > giornoMax)
                return giornoMax.Add(valore.TimeOfDay);

            return valore;
        }

        /// <summary>
        /// Riallinea i ticket aperti di tutte le fasi della commessa e salva. Va chiamato DOPO
        /// aver salvato le fasi: legge le finestre dal database, non dalle entita' in memoria.
        /// Restituisce quanti ticket ha spostato.
        /// </summary>
        public static async Task<int> SyncCommessaAsync(ApplicationDbContext context, int idCommessa)
        {
            var finestre = await context.CommessaFasi
                .AsNoTracking()
                .Where(f => f.IdCommessa == idCommessa)
                .Select(f => new { f.Id, f.StartDate, f.EndDate })
                .ToListAsync();

            var tickets = await context.Tickets
                .Where(t => t.IdCommessaFase != null
                    && t.CommessaFase!.IdCommessa == idCommessa
                    && !t.Closed)
                .ToListAsync();

            var perFase = finestre.ToDictionary(f => f.Id, f => (f.StartDate, f.EndDate));

            int spostati = 0;
            foreach (var ticket in tickets)
            {
                if (perFase.TryGetValue(ticket.IdCommessaFase!.Value, out var finestra)
                    && Apply(ticket, finestra.StartDate, finestra.EndDate))
                {
                    spostati++;
                }
            }

            if (spostati > 0)
                await context.SaveChangesAsync();

            return spostati;
        }
    }
}
