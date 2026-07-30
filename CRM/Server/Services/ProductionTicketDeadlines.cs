using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Regola unica sulle date di fine dei ticket di produzione: un ticket collegato a una fase di
    /// commessa finisce e scade quando finisce la fase. La fase e' il piano, il ticket ne e'
    /// l'esecuzione: date diverse vorrebbero dire che il Gantt e l'elenco ticket raccontano
    /// scadenze diverse, e il calcolo SLA per tipo ticket (giorni dall'apertura) non conosce la
    /// data di consegna. Vale solo sui ticket aperti: su un chiuso le date sono storia.
    /// </summary>
    internal static class ProductionTicketDeadlines
    {
        /// <summary>
        /// Allinea in memoria fine e scadenza del ticket alla fine della fase, tirando indietro
        /// anche l'inizio se resterebbe dopo la fine. Restituisce true se ha cambiato qualcosa:
        /// spostare una data rimette in coda il preavviso che la riguarda, altrimenti quello
        /// gia' inviato per la vecchia data varrebbe come inviato anche per la nuova.
        /// </summary>
        public static bool Apply(Ticket ticket, DateTime faseEnd)
        {
            var scadenza = faseEnd.Date;
            bool cambiato = false;

            if (ticket.DateExpired?.Date != scadenza)
            {
                ticket.DateExpired = scadenza;
                ticket.ReminderExpiryStatus = ReminderStatus.Pending;
                ticket.ReminderExpiryRetryCount = 0;
                ticket.ReminderExpiryLastAttemptAt = null;
                cambiato = true;
            }

            // La fine dell'appuntamento cambia giorno ma tiene l'ora: se qualcuno ha pianificato
            // il lavoro fino alle 17:30, spostare la fase non deve riportarlo a mezzanotte.
            var fine = ticket.DateEnd == null ? scadenza : scadenza.Add(ticket.DateEnd.Value.TimeOfDay);
            if (ticket.DateEnd != fine)
            {
                ticket.DateEnd = fine;
                cambiato = true;
            }

            // Riprogrammazione all'indietro: se la fase ora finisce prima dell'inizio pianificato,
            // l'appuntamento resterebbe a cavallo (inizio dopo la fine). L'inizio viene tirato
            // indietro con la stessa regola della fine — cambia il giorno, non l'ora — e il
            // preavviso dell'appuntamento torna in coda perche' riguarda un'altra data.
            if (ticket.Date is { } inizio && inizio.Date > scadenza)
            {
                ticket.Date = scadenza.Add(inizio.TimeOfDay);
                ticket.ReminderApptStatus = ReminderStatus.Pending;
                ticket.ReminderApptRetryCount = 0;
                ticket.ReminderApptLastAttemptAt = null;
                cambiato = true;
            }

            return cambiato;
        }

        /// <summary>
        /// Riallinea i ticket aperti di tutte le fasi della commessa e salva. Va chiamato DOPO
        /// aver salvato le fasi: legge le scadenze dal database, non dalle entita' in memoria.
        /// Restituisce quanti ticket ha spostato.
        /// </summary>
        public static async Task<int> SyncCommessaAsync(ApplicationDbContext context, int idCommessa)
        {
            var scadenze = await context.CommessaFasi
                .AsNoTracking()
                .Where(f => f.IdCommessa == idCommessa)
                .Select(f => new { f.Id, f.EndDate })
                .ToListAsync();

            return await SyncAsync(context, scadenze.ToDictionary(f => f.Id, f => f.EndDate));
        }

        /// <summary>
        /// Riallinea i ticket aperti delle sole fasi indicate e salva. Stessa avvertenza di
        /// <see cref="SyncCommessaAsync"/>: le fasi devono essere gia' salvate.
        /// </summary>
        public static async Task<int> SyncPhasesAsync(ApplicationDbContext context, ICollection<int> faseIds)
        {
            if (faseIds.Count == 0)
                return 0;

            var scadenze = await context.CommessaFasi
                .AsNoTracking()
                .Where(f => faseIds.Contains(f.Id))
                .Select(f => new { f.Id, f.EndDate })
                .ToListAsync();

            return await SyncAsync(context, scadenze.ToDictionary(f => f.Id, f => f.EndDate));
        }

        private static async Task<int> SyncAsync(ApplicationDbContext context, Dictionary<int, DateTime> scadenzePerFase)
        {
            if (scadenzePerFase.Count == 0)
                return 0;

            var faseIds = scadenzePerFase.Keys.ToList();
            var tickets = await context.Tickets
                .Where(t => t.IdCommessaFase != null
                    && faseIds.Contains(t.IdCommessaFase.Value)
                    && !t.Closed)
                .ToListAsync();

            int spostati = 0;
            foreach (var ticket in tickets)
            {
                if (scadenzePerFase.TryGetValue(ticket.IdCommessaFase!.Value, out var fine) && Apply(ticket, fine))
                    spostati++;
            }

            if (spostati > 0)
                await context.SaveChangesAsync();

            return spostati;
        }
    }
}
