using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Banco di prova per i test che passano dal database: contesto EF in memoria (isolato per ogni
/// test) e servizi di contorno sostituiti. Copre la logica che i test puri non possono toccare —
/// permessi di gruppo, blocco sui predecessori, generazione automatica dei ticket, rollup.
/// Restano fuori le parti legate a SQL Server (transazioni e SqlQueryRaw di CommesseService).
/// </summary>
public sealed class ProductionTestContext : IDisposable
{
    public ApplicationDbContext Db { get; }
    public IPermitsService Permits { get; }
    public CommessaFasiService Service { get; }

    public const string Utente = "utente-corrente";

    private ProductionTestContext(bool admin, int? gruppoUtente, List<int>? aziendeVisibili)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-test-{Guid.NewGuid()}")
            .Options;

        Db = new ApplicationDbContext(options);

        Permits = Substitute.For<IPermitsService>();
        Permits.IdUser().Returns(Utente);
        Permits.IsAdmin().Returns(admin);
        Permits.IsSuperUser().Returns(false);
        // null = nessun filtro di perimetro: vede tutte le aziende.
        Permits.GetVisibleCompanyIds().Returns(aziendeVisibili);

        if (gruppoUtente != null)
        {
            // Group.Users non è inizializzata nell'entità: va costruita qui.
            var gruppo = new Group
            {
                Id = gruppoUtente.Value,
                Name = $"Gruppo {gruppoUtente}",
                // Email è obbligatoria sull'utente: senza, l'inserimento viene rifiutato.
                Users = new List<ApplicationUser>
                {
                    new() { Id = Utente, UserName = Utente, Email = $"{Utente}@test.local" }
                }
            };
            Db.Groups.Add(gruppo);
            Db.SaveChanges();
        }

        Service = new CommessaFasiService(Db, Permits, Substitute.For<ILogEventService>());
    }

    /// <summary>Admin: nessun vincolo di gruppo né di perimetro aziende.</summary>
    public static ProductionTestContext ComeAdmin() => new(admin: true, gruppoUtente: null, aziendeVisibili: null);

    /// <summary>Utente standard che appartiene al gruppo indicato (nessuno se null).</summary>
    public static ProductionTestContext ComeUtenteDelGruppo(int? idGruppo)
        => new(admin: false, gruppoUtente: idGruppo, aziendeVisibili: null);

    // ─── Costruzione dello scenario ──────────────────────────────────────────

    public Commessa CreaCommessa(int id = 1, int idCompany = 1)
    {
        var c = new Commessa
        {
            Id = id,
            Code = $"CM-TEST-{id:D4}",
            IdCompany = idCompany,
            State = CommessaStates.Planned,
            StartDatePlanned = DateTime.Today,
            EndDatePlanned = DateTime.Today.AddDays(30),
            CreatedAt = DateTime.Now
        };
        Db.Commesse.Add(c);
        Db.SaveChanges();
        return c;
    }

    public CommessaFase CreaFase(
        int id,
        int idCommessa = 1,
        string nome = "Fase",
        int? idGruppo = null,
        int? idTicketType = null,
        int? parentId = null,
        CommessaFaseStates stato = CommessaFaseStates.Pending,
        CommessaFaseCompletionMode mode = CommessaFaseCompletionMode.AllTicketsClosed,
        bool requiresTicket = false,
        int progress = 0,
        int giorniDurata = 5)
    {
        var f = new CommessaFase
        {
            Id = id,
            IdCommessa = idCommessa,
            Name = nome,
            IdGroup = idGruppo,
            IdTicketType = idTicketType,
            ParentId = parentId,
            State = stato,
            CompletionMode = mode,
            RequiresTicket = requiresTicket,
            Progress = progress,
            SortOrder = id,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(giorniDurata - 1)
        };
        Db.CommessaFasi.Add(f);
        Db.SaveChanges();
        return f;
    }

    public void CreaDipendenza(int idFase, int idPredecessore, int lag = 0)
    {
        Db.CommessaFaseDependencies.Add(new CommessaFaseDependency
        {
            IdFase = idFase,
            IdPredecessorFase = idPredecessore,
            LagDays = lag
        });
        Db.SaveChanges();
    }

    public CommessaFaseTicketPlan CreaPianoTicket(
        int id,
        int idFase,
        ProductionTicketAutoCreateMode mode = ProductionTicketAutoCreateMode.OnPhaseStart,
        bool required = true,
        int? idGruppoAssegnato = null)
    {
        var p = new CommessaFaseTicketPlan
        {
            Id = id,
            IdCommessaFase = idFase,
            Title = $"Piano {id}",
            IdTicketType = 1,
            IdGroupAssigned = idGruppoAssegnato,
            Required = required,
            AutoCreateMode = mode,
            SortOrder = id
        };
        Db.CommessaFaseTicketPlans.Add(p);
        Db.SaveChanges();
        return p;
    }

    public Ticket CreaTicket(int id, int idFase, bool chiuso = false)
    {
        var t = new Ticket
        {
            Id = id,
            IdCommessaFase = idFase,
            Closed = chiuso,
            IdCompany = 1,
            IdUserOpened = Utente,
            DateOpened = DateTime.Now,
            Description = string.Empty,
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        };
        Db.Tickets.Add(t);
        Db.SaveChanges();
        return t;
    }

    /// <summary>Rilegge dal database, senza gli oggetti già tracciati in memoria.</summary>
    public CommessaFase Rileggi(int idFase)
    {
        Db.ChangeTracker.Clear();
        return Db.CommessaFasi.AsNoTracking().Single(f => f.Id == idFase);
    }

    public Commessa RileggiCommessa(int id = 1)
    {
        Db.ChangeTracker.Clear();
        return Db.Commesse.AsNoTracking().Single(c => c.Id == id);
    }

    public void Dispose() => Db.Dispose();
}
