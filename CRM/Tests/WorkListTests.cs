using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// L'elenco dei lavori: la schermata che il tecnico apre la mattina. Qui si verifica cosa ci entra e
/// in che ordine, perche' l'ordine e' il senso stesso della schermata: prima l'assistenza, poi le
/// commesse, e in fondo le fasi che non si possono ancora cominciare.
/// </summary>
public class WorkListTests : IDisposable
{
    private const string Io = "utente-corrente";
    private const string UnAltro = "altro-tecnico";
    private const int MioGruppo = 7;
    private const int GruppoAltrui = 8;

    private readonly ApplicationDbContext _db;
    private readonly TicketsService _service;

    public WorkListTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-worklist-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);

        // La ditta serve davvero: la proiezione dell'elenco passa dal legame Ticket -> Company, che
        // e' obbligatorio. Senza la riga della ditta il ticket viene scartato dalla query e l'elenco
        // torna vuoto senza dare errore.
        _db.Companies.Add(new Company { Id = 1, RagioneSociale = "Cliente di prova" });

        _db.Groups.Add(new Group
        {
            Id = MioGruppo,
            Name = "Meccanici",
            Users = new List<ApplicationUser>
            {
                new() { Id = Io, UserName = Io, Email = $"{Io}@test.local" }
            }
        });
        _db.Groups.Add(new Group { Id = GruppoAltrui, Name = "Elettricisti" });
        _db.SaveChanges();

        var permits = Substitute.For<IPermitsService>();
        permits.IdUser().Returns(Io);
        // null = nessun limite di azienda. Senza dirlo, il finto servizio risponde con una lista
        // vuota, che invece significa "nessuna azienda visibile" e scarta ogni ticket.
        permits.GetVisibleCompanyIds().Returns((List<int>?)null);

        _service = new TicketsService(
            _db,
            Substitute.For<IHttpContextAccessor>(),
            permits,
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null),
            Substitute.For<ILogEventService>(),
            Substitute.For<ILanguagesService>(),
            Substitute.For<ICommessaFasiService>(),
            Substitute.For<ITicketBlockNotificationService>());
    }

    private void CreaFase(int id, DateTime fine, CommessaFaseStates stato = CommessaFaseStates.Pending, int? dipendeDa = null)
    {
        _db.CommessaFasi.Add(new CommessaFase
        {
            Id = id,
            IdCommessa = 1,
            Name = $"Fase {id}",
            State = stato,
            StartDate = fine.AddDays(-5),
            EndDate = fine
        });

        if (dipendeDa != null)
        {
            _db.CommessaFaseDependencies.Add(new CommessaFaseDependency
            {
                Id = id,
                IdFase = id,
                IdPredecessorFase = dipendeDa.Value
            });
        }

        _db.SaveChanges();
    }

    private void CreaTicket(
        int id,
        DateTime? data = null,
        DateTime? scadenza = null,
        int? idFase = null,
        string? assegnatoA = null,
        int? gruppo = null,
        bool chiuso = false)
    {
        _db.Tickets.Add(new Ticket
        {
            Id = id,
            IdCompany = 1,
            IdType = 1,
            Date = data,
            DateExpired = scadenza,
            IdCommessaFase = idFase,
            IdUserAssigned = assegnatoA,
            IdGroupAssigned = gruppo,
            Closed = chiuso,
            DateOpened = DateTime.Today,
            Description = $"Lavoro {id}",
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        });
        _db.SaveChanges();
    }

    private async Task<List<(int Id, WorkListGroup Gruppo)>> ElencoAsync()
    {
        var risposta = await _service.GetWorkListAsync();
        Assert.Null(risposta.ErrorMessage);
        return risposta.Items.Select(i => (i.IdTicket, i.Group)).ToList();
    }

    [Fact]
    public async Task Prima_l_assistenza_poi_le_commesse_e_in_fondo_le_fasi_bloccate()
    {
        CreaFase(1, DateTime.Today.AddDays(10));                                   // avviabile
        CreaFase(2, DateTime.Today.AddDays(20), dipendeDa: 1);                     // bloccata da 1

        CreaTicket(30, idFase: 2, assegnatoA: Io);        // fase bloccata
        CreaTicket(31, idFase: 1, assegnatoA: Io);        // fase avviabile
        CreaTicket(32, data: DateTime.Today, assegnatoA: Io); // assistenza

        Assert.Equal(
            new[]
            {
                (32, WorkListGroup.Assistenza),
                (31, WorkListGroup.Commessa),
                (30, WorkListGroup.CommessaBloccata)
            },
            await ElencoAsync());
    }

    [Fact]
    public async Task Una_fase_con_i_predecessori_finiti_non_e_bloccata()
    {
        CreaFase(1, DateTime.Today.AddDays(10), stato: CommessaFaseStates.Done);
        CreaFase(2, DateTime.Today.AddDays(20), dipendeDa: 1);

        CreaTicket(30, idFase: 2, assegnatoA: Io);

        Assert.Equal(new[] { (30, WorkListGroup.Commessa) }, await ElencoAsync());
    }

    /// <summary>Dentro le commesse comanda la scadenza: chiude prima, sta piu' in alto.</summary>
    [Fact]
    public async Task I_lavori_di_commessa_sono_ordinati_per_scadenza()
    {
        CreaFase(1, DateTime.Today.AddDays(30));
        CreaFase(2, DateTime.Today.AddDays(5));

        CreaTicket(30, scadenza: DateTime.Today.AddDays(30), idFase: 1, assegnatoA: Io);
        CreaTicket(31, scadenza: DateTime.Today.AddDays(5), idFase: 2, assegnatoA: Io);

        Assert.Equal(new[] { 31, 30 }, (await ElencoAsync()).Select(x => x.Id));
    }

    /// <summary>
    /// L'assistenza ha una data decisa da chi assegna. Quella di domani non serve stamattina;
    /// quella di ieri, rimasta aperta, si'.
    /// </summary>
    [Fact]
    public async Task L_assistenza_futura_non_compare_quella_arretrata_si()
    {
        CreaTicket(30, data: DateTime.Today.AddDays(-3), assegnatoA: Io);
        CreaTicket(31, data: DateTime.Today.AddDays(3), assegnatoA: Io);

        Assert.Equal(new[] { 30 }, (await ElencoAsync()).Select(x => x.Id));
    }

    /// <summary>
    /// Un ticket fermo sul proprio gruppo e' lavoro proprio: se non lo prende lui non lo prende
    /// nessuno. Quello di un altro gruppo, o gia' in mano a un altro, non lo riguarda.
    /// </summary>
    [Fact]
    public async Task Compaiono_i_lavori_del_proprio_gruppo_non_ancora_presi()
    {
        CreaTicket(30, data: DateTime.Today, gruppo: MioGruppo);
        CreaTicket(31, data: DateTime.Today, gruppo: GruppoAltrui);
        CreaTicket(32, data: DateTime.Today, assegnatoA: UnAltro);

        Assert.Equal(new[] { 30 }, (await ElencoAsync()).Select(x => x.Id));
    }

    [Fact]
    public async Task I_ticket_chiusi_non_compaiono()
    {
        CreaTicket(30, data: DateTime.Today, assegnatoA: Io, chiuso: true);

        Assert.Empty(await ElencoAsync());
    }

    public void Dispose() => _db.Dispose();
}
