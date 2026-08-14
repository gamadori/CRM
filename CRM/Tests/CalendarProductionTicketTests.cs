using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using ClientLogEventService = CRM.Client.Services.ILogEventService;

namespace CRM.Tests;

/// <summary>
/// Un ticket di una fase di commessa entra in agenda, ma non sul calendario.
/// <para>
/// La sua data non e' un momento in cui il tecnico e' occupato: e' la scadenza della fase. Disegnarlo
/// sul calendario ha prodotto prima un blocco continuo per tutta la fase, poi - tolta la fine -
/// un'ora a mezzanotte. Due affermazioni false. Deve pero' restare nell'elenco delle scadenze, che e'
/// dove si guarda cosa c'e' da fare: per questo la distinzione e' un campo del dato e non
/// un'esclusione alla fonte.
/// </para>
/// </summary>
public class CalendarProductionTicketTests : IDisposable
{
    private const string Utente = "utente-corrente";

    private readonly ApplicationDbContext _db;
    private readonly CalendarService _service;

    public CalendarProductionTicketTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-calendar-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);

        // La ditta va creata: il caricamento passa dal legame Ticket -> Company, che e' obbligatorio.
        // Senza, il ticket sparisce dalla query e l'agenda torna vuota senza dare errore.
        _db.Companies.Add(new Company { Id = 1, RagioneSociale = "Cliente di prova" });
        _db.SaveChanges();

        var permits = Substitute.For<IPermitsService>();
        permits.IdUser().Returns(Utente);
        permits.GetIdCompanies().Returns(new List<int> { 1 });
        permits.CanEditTicket().Returns(true);

        _service = new CalendarService(_db, permits, Substitute.For<ClientLogEventService>());
    }

    private void CreaTicket(int id, DateTime data, DateTime scadenza, int? idFase = null)
    {
        _db.Tickets.Add(new Ticket
        {
            Id = id,
            IdCompany = 1,
            IdType = 1,
            IdCommessaFase = idFase,
            IdUserAssigned = Utente,
            Date = data,
            DateExpired = scadenza,
            DateOpened = data,
            Description = string.Empty,
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        });
        _db.SaveChanges();
    }

    private async Task<List<CalendarItemDTO>> AgendaAsync()
    {
        var agenda = await _service.GetAgendaAsync(new CalendarFilter
        {
            DateFrom = new DateTime(2026, 8, 1),
            DateTo = new DateTime(2026, 9, 30),
            IncludeActivities = false,
            IncludeInitiatives = false,
            IncludeTickets = true
        });

        // GetAgendaAsync inghiotte le eccezioni e torna un elenco vuoto: senza questo controllo un
        // test "non compare" passerebbe anche per un errore del servizio.
        Assert.Null(agenda.ErrorMessage);
        return agenda.Items;
    }

    [Fact]
    public async Task Il_ticket_di_fase_c_e_ma_non_va_sul_calendario()
    {
        _db.CommessaFasi.Add(new CommessaFase
        {
            Id = 7,
            IdCommessa = 1,
            Name = "Assemblaggio",
            StartDate = new DateTime(2026, 8, 21),
            EndDate = new DateTime(2026, 8, 27)
        });
        _db.SaveChanges();

        CreaTicket(10, data: new DateTime(2026, 8, 21), scadenza: new DateTime(2026, 8, 27), idFase: 7);

        var voce = Assert.Single(await AgendaAsync());
        Assert.False(voce.PlacedOnCalendar);
        // La data utile e' la scadenza della fase, non il giorno da cui si puo' cominciare.
        Assert.Equal(new DateTime(2026, 8, 27), voce.Start);
    }

    /// <summary>
    /// Contro-prova: senza questa, il test qui sopra passerebbe anche se l'agenda non caricasse
    /// alcun ticket.
    /// </summary>
    [Fact]
    public async Task Un_ticket_di_assistenza_resta_collocabile_sul_calendario()
    {
        CreaTicket(11, data: new DateTime(2026, 8, 21), scadenza: new DateTime(2026, 8, 24));

        var voce = Assert.Single(await AgendaAsync());
        Assert.True(voce.PlacedOnCalendar);
        Assert.Equal(new DateTime(2026, 8, 21), voce.Start);
    }

    public void Dispose() => _db.Dispose();
}
