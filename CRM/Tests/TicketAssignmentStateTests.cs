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
/// Assegnare un ticket e avviarne la lavorazione sono due passaggi diversi. Questi test difendono
/// il confine: Claim e assegnazioni devono arrivare ad Assigned, non a Processing.
/// </summary>
public class TicketAssignmentStateTests : IDisposable
{
    private const int IdCompany = 1;
    private const int IdType = 1;
    private const string CurrentUser = "utente-corrente";
    private const string TechUser = "tecnico";

    private readonly ApplicationDbContext _db;
    private readonly TicketsService _service;
    private readonly IPermitsService _permits;

    public TicketAssignmentStateTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-ticket-assignment-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        SeedBaseData();

        _permits = Substitute.For<IPermitsService>();
        _permits.IdUser().Returns(CurrentUser);
        _permits.IsAdmin().Returns(true);
        _permits.IsSuperUser().Returns(false);
        _permits.IsClient().Returns(false);
        _permits.CanGetObject(Arg.Any<int?>()).Returns(true);
        _permits.CanViewInternalData().Returns(true);
        _permits.GetVisibleCompanyIds().Returns((List<int>?)null);
        _permits.TicketPermits(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        _permits.TicketPermits(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(0);
        _permits.CanReceveTicket(Arg.Any<int>(), Arg.Any<string>()).Returns(true);

        _service = new TicketsService(
            _db,
            Substitute.For<IHttpContextAccessor>(),
            _permits,
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null),
            Substitute.For<ILogEventService>(),
            Substitute.For<ILanguagesService>(),
            Substitute.For<ICommessaFasiService>(),
            Substitute.For<ITicketBlockNotificationService>());
    }

    [Fact]
    public async Task Claim_assegna_ma_non_avvia_la_lavorazione()
    {
        CreaTicket(1, StatoId(eTicketStates.Created));

        var result = await _service.ClaimAsync(1, CurrentUser);

        Assert.True(result.State);
        Assert.Equal(StatoId(eTicketStates.Assigned), Rileggi(1).IdState);
    }

    [Fact]
    public async Task Assegnazione_implicita_da_intervento_resta_assigned()
    {
        CreaTicket(2, StatoId(eTicketStates.Created));

        var result = await _service.EnsureUsersAssignedAsync(2, new[] { TechUser }, CurrentUser);

        Assert.True(result.Success);
        Assert.Equal(StatoId(eTicketStates.Assigned), Rileggi(2).IdState);
    }

    /// <summary>
    /// Registrare un intervento assegna chi ci ha lavorato e lascia il ticket "assegnato". Prima lo
    /// portava in uno stato "in lavorazione" a se': quello stato dentro l'azienda non esiste piu',
    /// perche' un ticket assegnato e' gia' un ticket su cui si lavora.
    /// </summary>
    [Fact]
    public async Task Registrare_lavoro_sul_ticket_lo_lascia_assegnato()
    {
        CreaTicket(20, StatoId(eTicketStates.Assigned), TechUser);

        var result = await _service.StartWorkAsync(20, new[] { TechUser }, CurrentUser);

        Assert.True(result);
        Assert.Equal(StatoId(eTicketStates.Assigned), Rileggi(20).IdState);
    }

    [Fact]
    public async Task Registrare_lavoro_assegna_l_operatore_se_il_ticket_non_ha_utenti()
    {
        CreaTicket(21, StatoId(eTicketStates.Created));

        var result = await _service.StartWorkAsync(21, null, CurrentUser);

        var ticket = Rileggi(21);
        Assert.True(result);
        Assert.Equal(CurrentUser, ticket.IdUserAssigned);
        Assert.Equal(StatoId(eTicketStates.Assigned), ticket.IdState);
    }

    [Fact]
    public async Task Assegnare_utenti_porta_il_ticket_ad_assegnato()
    {
        CreaTicket(3, StatoId(eTicketStates.Created));

        var result = await _service.AssignUsersAsync(3, new AssignUsersRequest { UserIds = new List<string> { TechUser } }, CurrentUser);

        Assert.True(result.Success);
        Assert.Equal(StatoId(eTicketStates.Assigned), Rileggi(3).IdState);
    }

    [Fact]
    public async Task Togliere_l_ultimo_utente_riporta_il_ticket_ad_aperto()
    {
        CreaTicket(4, StatoId(eTicketStates.Assigned), TechUser);

        var result = await _service.AssignUsersAsync(4, new AssignUsersRequest { UserIds = new List<string>() }, CurrentUser);

        Assert.True(result.Success);
        Assert.Equal(StatoId(eTicketStates.Created), Rileggi(4).IdState);
    }

    [Fact]
    public async Task Filtro_assigned_mostra_solo_ticket_aperti_assegnati()
    {
        CreaTicket(30, StatoId(eTicketStates.Created));
        CreaTicket(31, StatoId(eTicketStates.Assigned), TechUser);
        CreaTicket(32, StatoId(eTicketStates.Closed), TechUser, closed: true);

        var result = await _service.GetPagingAsync(new TicketFilter
        {
            TypeSearch = (int)TicketTypeSearch.Assigned,
            PageSize = 10,
            Skip = 0,
            Top = 10
        });

        Assert.Equal(new[] { 31 }, result.Items.Select(t => t.Id).OrderBy(id => id));
    }

    private void SeedBaseData()
    {
        _db.Companies.Add(new Company { Id = IdCompany, RagioneSociale = "Cliente test" });
        _db.TicketTypes.Add(new TicketType { Id = IdType, Desc = "Assistenza" });
        _db.Users.AddRange(
            new ApplicationUser { Id = CurrentUser, UserName = CurrentUser, Email = $"{CurrentUser}@test.local" },
            new ApplicationUser { Id = TechUser, UserName = TechUser, Email = $"{TechUser}@test.local" });

        _db.TicketStates.AddRange(
            new TicketState { Id = 1, State = (int)eTicketStates.Created, Description = "Aperto", Color = "#0d6efd" },
            new TicketState { Id = 2, State = (int)eTicketStates.Assigned, Description = "Assegnato", Color = "#6f42c1" },
            new TicketState { Id = 3, State = (int)eTicketStates.Processing, Description = "In lavorazione", Color = "#fd7e14" },
            new TicketState { Id = 4, State = (int)eTicketStates.Expired, Description = "Scaduto", Color = "#dc3545" },
            new TicketState { Id = 5, State = (int)eTicketStates.Closed, Description = "Chiuso", Color = "#198754" });

        _db.SaveChanges();
    }

    private Ticket CreaTicket(int id, int idState, string? assignedUser = null, bool closed = false)
    {
        var ticket = new Ticket
        {
            Id = id,
            IdCompany = IdCompany,
            IdType = IdType,
            IdState = idState,
            IdUserAssigned = assignedUser,
            IdUserOpened = CurrentUser,
            Date = DateTime.Today,
            DateOpened = DateTime.Today,
            Closed = closed,
            Description = string.Empty,
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        };

        _db.Tickets.Add(ticket);

        if (!string.IsNullOrWhiteSpace(assignedUser))
        {
            _db.TicketUserAssignments.Add(new TicketUserAssignment
            {
                IdTicket = id,
                IdUser = assignedUser,
                AssignedDate = DateTime.Now,
                AssignedBy = CurrentUser
            });
        }

        _db.SaveChanges();
        return ticket;
    }

    private int StatoId(eTicketStates state)
        => _db.TicketStates.Single(s => s.State == (int)state).Id;

    private Ticket Rileggi(int id)
    {
        _db.ChangeTracker.Clear();
        return _db.Tickets.AsNoTracking().Single(t => t.Id == id);
    }

    public void Dispose() => _db.Dispose();
}
