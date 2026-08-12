using System;
using System.Linq.Expressions;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;

namespace CRM.Server.Services
{
    /// <summary>
    /// Quanto dura un segmento di intervento. Definizione unica: prima la stessa domanda aveva
    /// sette risposte sparse, scritte in due modi diversi.
    /// <para>
    /// I due modi restano, e non e' una svista: <see cref="Sql"/> vive dentro una query e viene
    /// tradotta in <c>DATEDIFF(minute, ...)</c>, cosi' i totali si calcolano sul database senza
    /// portarsi in memoria migliaia di segmenti; <see cref="InMemoria"/> lavora su entita' gia'
    /// caricate, ed e' l'unica strada dove la query non c'e' o dove il provider non e' SQL Server
    /// (il verbale PDF, le ore di commessa, i test con il database in memoria). La differenza fra
    /// due <c>DateTime</c> non e' traducibile in SQL e <c>DateDiffMinute</c> esiste solo su SQL
    /// Server: nessuna delle due copre entrambi i casi.
    /// </para>
    /// <para>
    /// <b>Danno la stessa risposta solo perche' i secondi sono sempre zero.</b> SQL conta i confini
    /// di minuto attraversati, .NET tronca il tempo trascorso: da 10:00:59 a 10:01:00 la prima dice
    /// un minuto e la seconda zero. A garantirlo e'
    /// <c>ApplicationDbContext.NormalizeInterventionTimes</c>, che tronca in salvataggio - se
    /// qualcuno lo togliesse, queste due smetterebbero di essere intercambiabili e il verbale
    /// firmato dal cliente comincerebbe a non tornare con il totale del ticket.
    /// </para>
    /// </summary>
    internal static class InterventionMinutes
    {
        /// <summary>
        /// Durata di un segmento gia' caricato in memoria.
        /// </summary>
        public static int InMemoria(DateTime inizio, DateTime fine)
            => (int)(fine - inizio).TotalMinutes;

        /// <summary>Come sopra, direttamente da un segmento.</summary>
        public static int InMemoria(TicketInterventionTime segmento)
            => InMemoria(segmento.StartDateTime, segmento.EndDateTime);

        /// <summary>
        /// Durata di un segmento calcolata dal database. Va passata a <c>Sum</c>/<c>Select</c> di
        /// una query: essendo un'espressione, EF la traduce. Richiamarla come un metodo normale
        /// dentro una lambda la renderebbe invece intraducibile, ed e' il motivo per cui questa e'
        /// un'espressione e non un metodo.
        /// </summary>
        public static readonly Expression<Func<TicketInterventionTime, int>> Sql =
            t => EF.Functions.DateDiffMinute(t.StartDateTime, t.EndDateTime);
    }
}
