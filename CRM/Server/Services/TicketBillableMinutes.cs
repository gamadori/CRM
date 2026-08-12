using CRM.Server.Data;
using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Helper;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Definizione unica delle ore di un ticket: i minuti dei tempi **fatturabili** registrati
    /// sugli interventi (<see cref="TicketInterventionTime"/> con <c>IsBillable</c>), sommati per
    /// tipo. Prima la lista contava i soli segmenti di lavoro, quindi diceva meno della scheda,
    /// che sommava <c>TicketIntervention.Minute</c> — cioe' il totale fatturabile, viaggio
    /// compreso, denormalizzato da <c>UpdateInterventionTotalMinutes</c>.
    /// Qui si riparte dai segmenti invece di leggere quel campo per due motivi: e' l'unico modo
    /// di avere la ripartizione per tipo, e non dipende da una cache denormalizzata che una
    /// scrittura distratta puo' lasciare indietro.
    /// </summary>
    internal static class TicketBillableMinutes
    {
        /// <summary>Minuti fatturabili di un ticket, divisi per tipo di tempo.</summary>
        public sealed class Breakdown
        {
            public int Work { get; set; }
            public int Travel { get; set; }
            public int Break { get; set; }

            public int Total => Work + Travel + Break;
        }

        /// <summary>Totale complessivo di un insieme di ticket (il piede della lista, che conta
        /// tutti i ticket filtrati e non solo la pagina visibile).</summary>
        public static async Task<int> TotalAsync(ApplicationDbContext context, IQueryable<Ticket> tickets)
            => await context.TicketsInterventions
                .Where(i => tickets.Contains(i.Ticket))
                .SelectMany(i => i.TicketInterventionTime)
                .Where(t => t.IsBillable)
                .SumAsync(InterventionMinutes.Sql);

        /// <summary>Ripartizione per un solo ticket (mai null: un ticket senza tempi vale zero).</summary>
        public static async Task<Breakdown> ForTicketAsync(ApplicationDbContext context, int idTicket)
        {
            var perTicket = await ByTicketAsync(context, new[] { idTicket });
            return perTicket.TryGetValue(idTicket, out var b) ? b : new Breakdown();
        }

        /// <summary>
        /// Ripartizione per piu' ticket in una query sola: la lista la chiama una volta per pagina,
        /// non una volta per riga. I ticket senza tempi fatturabili non compaiono nel risultato.
        /// </summary>
        public static async Task<Dictionary<int, Breakdown>> ByTicketAsync(ApplicationDbContext context, ICollection<int> ticketIds)
        {
            var result = new Dictionary<int, Breakdown>();
            if (ticketIds.Count == 0)
                return result;

            var righe = await context.TicketsInterventions
                .Where(i => ticketIds.Contains(i.IdTicket))
                .SelectMany(i => i.TicketInterventionTime.Select(t => new
                {
                    i.IdTicket,
                    t.TimeType,
                    t.IsBillable,
                    t.StartDateTime,
                    t.EndDateTime
                }))
                .Where(x => x.IsBillable)
                .GroupBy(x => new { x.IdTicket, x.TimeType })
                .Select(g => new
                {
                    g.Key.IdTicket,
                    g.Key.TimeType,
                    // Unico punto rimasto con la formula in chiaro: qui si somma dentro un
                    // raggruppamento su una proiezione anonima, dove l'espressione di
                    // InterventionMinutes.Sql non e' riutilizzabile. Deve restare identica a quella.
                    Minuti = g.Sum(x => (int)EF.Functions.DateDiffMinute(x.StartDateTime, x.EndDateTime))
                })
                .ToListAsync();

            foreach (var riga in righe)
            {
                if (!result.TryGetValue(riga.IdTicket, out var b))
                {
                    b = new Breakdown();
                    result[riga.IdTicket] = b;
                }

                switch (riga.TimeType)
                {
                    case InterventionTimeType.Work: b.Work += riga.Minuti; break;
                    case InterventionTimeType.Travel: b.Travel += riga.Minuti; break;
                    case InterventionTimeType.Break: b.Break += riga.Minuti; break;
                }
            }

            return result;
        }

        /// <summary>
        /// Riallinea <see cref="TicketIntervention.Minute"/>, che e' la copia denormalizzata dei
        /// minuti fatturabili del singolo intervento (la leggono la griglia interventi e le firme
        /// in attesa). Va chiamata da OGNI scrittura sui tempi: chi la salta lascia il totale
        /// dell'intervento fermo al valore precedente, e nessuno se ne accorge finche' qualcuno
        /// non confronta i numeri. Usa la stessa DATEDIFF del calcolo per ticket, cosi' cache e
        /// totale non possono divergere per arrotondamento.
        /// </summary>
        public static async Task RecalculateInterventionAsync(ApplicationDbContext context, int idIntervention)
        {
            if (idIntervention <= 0)
                return;

            var intervention = await context.TicketsInterventions.FindAsync(idIntervention);
            if (intervention == null)
                return;

            var minuti = await context.TicketInterventionTimes
                .Where(t => t.IdTicketIntervention == idIntervention && t.IsBillable)
                .SumAsync(InterventionMinutes.Sql);

            if (intervention.Minute == minuti)
                return;

            intervention.Minute = minuti;
            await context.SaveChangesAsync();
        }

        /// <summary>Riversa la ripartizione sul DTO, formattati compresi.</summary>
        public static void ApplyTo(TicketDTO dto, Breakdown b)
        {
            dto.MinuteWork = b.Work;
            dto.MinuteTravel = b.Travel;
            dto.MinuteBreak = b.Break;
            dto.MinuteBillable = b.Total;

            dto.MinuteWorkFormatted = DateTimeHelper.MinuteFormat(b.Work);
            dto.MinuteTravelFormatted = DateTimeHelper.MinuteFormat(b.Travel);
            dto.MinuteBillableFormatted = DateTimeHelper.MinuteFormat(b.Total);
            dto.MinuteTotalFormatted = dto.MinuteBillableFormatted;
        }
    }
}
