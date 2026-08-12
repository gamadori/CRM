using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRM.Tests;

/// <summary>
/// I tempi di intervento si salvano sempre al minuto tondo.
/// <para>
/// E' l'invariante da cui dipende la coerenza dei totali, perche' i minuti di un segmento si
/// calcolano in <b>due modi diversi</b> a seconda di chi chiede: <c>DATEDIFF(minute, ...)</c> lato
/// SQL per i totali del ticket e per la cache <c>TicketIntervention.Minute</c>, la differenza fra
/// i due orari in memoria per il verbale PDF e per le ore di commessa. Le due strade rispondono
/// uguale <b>solo</b> se i secondi sono zero: SQL conta i confini di minuto attraversati, .NET
/// tronca il tempo trascorso, e da 10:00:59 a 10:01:00 la prima dice un minuto e la seconda zero.
/// </para>
/// <para>
/// Il sintomo, se l'invariante salta, non e' un errore: e' il verbale firmato dal cliente che non
/// torna con il totale del ticket, di pochi minuti, e nessuno che sappia dire quale dei due mente.
/// </para>
/// </summary>
public class InterventionTimeRoundingTests
{
    private static ApplicationDbContext Db(string nome) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task Un_segmento_nuovo_perde_i_secondi_nel_salvataggio()
    {
        var nome = $"crm-tempi-{Guid.NewGuid()}";

        using (var db = Db(nome))
        {
            db.TicketInterventionTimes.Add(new TicketInterventionTime
            {
                IdTicketIntervention = 1,
                StartDateTime = new DateTime(2026, 8, 12, 10, 0, 59, 500),
                EndDateTime = new DateTime(2026, 8, 12, 10, 1, 0, 250),
                TimeType = InterventionTimeType.Work,
                IsBillable = true
            });

            await db.SaveChangesAsync();
        }

        using var verifica = Db(nome);
        var salvato = verifica.TicketInterventionTimes.Single();

        Assert.Equal(new DateTime(2026, 8, 12, 10, 0, 0), salvato.StartDateTime);
        Assert.Equal(new DateTime(2026, 8, 12, 10, 1, 0), salvato.EndDateTime);
    }

    [Fact]
    public async Task Vale_anche_quando_si_modifica_un_segmento_esistente()
    {
        var nome = $"crm-tempi-{Guid.NewGuid()}";
        int id;

        using (var db = Db(nome))
        {
            var segmento = new TicketInterventionTime
            {
                IdTicketIntervention = 1,
                StartDateTime = new DateTime(2026, 8, 12, 9, 0, 0),
                EndDateTime = new DateTime(2026, 8, 12, 9, 30, 0),
                TimeType = InterventionTimeType.Work
            };

            db.TicketInterventionTimes.Add(segmento);
            await db.SaveChangesAsync();
            id = segmento.Id;
        }

        using (var db = Db(nome))
        {
            var segmento = db.TicketInterventionTimes.Single(x => x.Id == id);
            segmento.EndDateTime = new DateTime(2026, 8, 12, 9, 47, 33);
            await db.SaveChangesAsync();
        }

        using var verifica = Db(nome);

        Assert.Equal(0, verifica.TicketInterventionTimes.Single().EndDateTime.Second);
    }

    [Fact]
    public async Task Il_troncamento_non_tocca_le_altre_entita()
    {
        // La normalizzazione guarda un tipo solo: se domani diventasse "tronca tutte le date"
        // arrotonderebbe in silenzio orari che devono restare al secondo.
        var nome = $"crm-tempi-{Guid.NewGuid()}";
        var conSecondi = new DateTime(2026, 8, 12, 10, 0, 59);

        using (var db = Db(nome))
        {
            db.TicketsInterventions.Add(new TicketIntervention
            {
                IdTicket = 1,
                IdUser = "utente",
                Activities = "prova",
                StartDateTime = conSecondi,
                EndDateTime = conSecondi.AddHours(1)
            });

            await db.SaveChangesAsync();
        }

        using var verifica = Db(nome);

        Assert.Equal(59, verifica.TicketsInterventions.Single().StartDateTime.Second);
    }

    // ─── Perche' l'invariante serve ──────────────────────────────────────────

    /// <summary>Quello che fa SQL: conta i confini di minuto attraversati.</summary>
    private static int ComeSql(DateTime inizio, DateTime fine)
        => (int)(fine.Date.AddMinutes(fine.Hour * 60 + fine.Minute)
               - inizio.Date.AddMinutes(inizio.Hour * 60 + inizio.Minute)).TotalMinutes;

    /// <summary>
    /// Quello che fa .NET in memoria: tronca il tempo trascorso. Non e' una copia della formula,
    /// e' proprio il metodo di produzione - cosi' se cambia, questi test lo dicono.
    /// </summary>
    private static int ComeDotNet(DateTime inizio, DateTime fine)
        => CRM.Server.Services.InterventionMinutes.InMemoria(inizio, fine);

    [Fact]
    public void Con_i_secondi_le_due_formule_non_dicono_la_stessa_cosa()
    {
        var inizio = new DateTime(2026, 8, 12, 10, 0, 59);
        var fine = new DateTime(2026, 8, 12, 10, 1, 0);

        Assert.Equal(1, ComeSql(inizio, fine));
        Assert.Equal(0, ComeDotNet(inizio, fine));
    }

    [Theory]
    [InlineData(0, 0, 30, 0)]
    [InlineData(0, 59, 0, 1)]      // il caso che senza troncamento sballa
    [InlineData(15, 33, 47, 12)]
    [InlineData(0, 1, 59, 59)]
    public void Azzerati_i_secondi_le_due_formule_coincidono(
        int minutiInizio, int secondiInizio, int minutiFine, int secondiFine)
    {
        var inizio = new DateTime(2026, 8, 12, 10, minutiInizio, secondiInizio);
        var fine = new DateTime(2026, 8, 12, 11, minutiFine, secondiFine);

        var inizioTondo = new DateTime(inizio.Year, inizio.Month, inizio.Day, inizio.Hour, inizio.Minute, 0);
        var fineTonda = new DateTime(fine.Year, fine.Month, fine.Day, fine.Hour, fine.Minute, 0);

        Assert.Equal(ComeSql(inizioTondo, fineTonda), ComeDotNet(inizioTondo, fineTonda));
    }
}
