using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Extensions;
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
/// Chi decide la scadenza di un ticket. Il calcolo SLA per tipo (apertura + N giorni lavorativi)
/// vale per l'assistenza; per un ticket di produzione la scadenza e' la fine della fase di commessa,
/// e il calcolo per tipo non conosce il piano.
/// <para>
/// Il difetto che questi test presidiano: il riallineamento delle scadenze gira a ogni caricamento
/// di elenco e a ogni apertura di scheda, su tutti i ticket aperti, e <b>salva</b>. Non escludendo
/// quelli di produzione, bastava aprire l'elenco ticket perche' la data di consegna del Gantt venisse
/// sostituita da "apertura + 3 giorni" - in silenzio, e lasciando DateEnd sulla fase, cioe' i due
/// campi in disaccordo.
/// </para>
/// </summary>
public class TicketExpiryRefreshTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly TicketsService _service;

    public TicketExpiryRefreshTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-expiry-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);

        var permits = Substitute.For<IPermitsService>();
        permits.IdUser().Returns("utente-corrente");

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

    /// <summary>Lunedi' prossimo: ancorare il giorno evita che il conteggio dei giorni lavorativi
    /// dia un risultato diverso a seconda del giorno in cui girano i test.</summary>
    private static DateTime ProssimoLunedi
        => DateTime.Today.AddDays(((int)DayOfWeek.Monday - (int)DateTime.Today.DayOfWeek + 7) % 7);

    private Ticket CreaTicket(int id, DateTime apertura, DateTime scadenza, int? idFase = null)
    {
        var ticket = new Ticket
        {
            Id = id,
            IdCompany = 1,
            IdType = 1,
            IdCommessaFase = idFase,
            Date = apertura,
            DateExpired = scadenza,
            DateOpened = apertura,
            Description = string.Empty,
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        };
        _db.Tickets.Add(ticket);
        _db.SaveChanges();
        return ticket;
    }

    private Ticket Rileggi(int id)
    {
        _db.ChangeTracker.Clear();
        return _db.Tickets.AsNoTracking().Single(t => t.Id == id);
    }

    [Fact]
    public async Task Aprire_un_ticket_di_produzione_non_ne_riscrive_la_scadenza()
    {
        _db.CommessaFasi.Add(new CommessaFase
        {
            Id = 7,
            IdCommessa = 1,
            Name = "Sviluppo",
            StartDate = ProssimoLunedi,
            EndDate = ProssimoLunedi.AddDays(30)
        });
        _db.SaveChanges();

        // La scadenza e' la fine della fase, lontanissima dai 3 giorni della SLA per tipo.
        var scadenzaDiFase = ProssimoLunedi.AddDays(30);
        CreaTicket(1, apertura: ProssimoLunedi, scadenza: scadenzaDiFase, idFase: 7);

        await _service.SetTicketStateAsync(new TicketDTO { Id = 1, IdCompany = 1 });

        Assert.Equal(scadenzaDiFase, Rileggi(1).DateExpired);
    }

    /// <summary>
    /// Contro-prova: senza questa, il test qui sopra passerebbe anche se il riallineamento non
    /// girasse affatto, e non proverebbe nulla.
    /// </summary>
    [Fact]
    public async Task Su_un_ticket_di_assistenza_la_scadenza_viene_riallineata_alla_SLA()
    {
        CreaTicket(2, apertura: ProssimoLunedi, scadenza: ProssimoLunedi.AddYears(-1));

        await _service.SetTicketStateAsync(new TicketDTO { Id = 2, IdCompany = 1 });

        // Nessun TicketType e nessuna GlobalSetting in archivio: il ripiego e' 3 giorni.
        Assert.Equal(ProssimoLunedi.AddWorkdays(3), Rileggi(2).DateExpired);
    }

    /// <summary>
    /// La scadenza si e' mossa, quindi il preavviso gia' inviato riguardava un'altra data: se
    /// restasse Sent, quello per la nuova non partirebbe mai.
    /// </summary>
    [Fact]
    public async Task Riallineare_la_scadenza_rimette_in_coda_il_preavviso()
    {
        var ticket = CreaTicket(3, apertura: ProssimoLunedi, scadenza: ProssimoLunedi.AddYears(-1));
        ticket.ReminderExpiryStatus = ReminderStatus.Sent;
        ticket.ReminderExpiryRetryCount = 2;
        ticket.ReminderExpiryLastAttemptAt = DateTime.Now.AddDays(-1);
        _db.SaveChanges();

        await _service.SetTicketStateAsync(new TicketDTO { Id = 3, IdCompany = 1 });

        var aggiornato = Rileggi(3);
        Assert.Equal(ReminderStatus.Pending, aggiornato.ReminderExpiryStatus);
        Assert.Equal(0, aggiornato.ReminderExpiryRetryCount);
        Assert.Null(aggiornato.ReminderExpiryLastAttemptAt);
    }

    /// <summary>
    /// La scadenza gia' giusta non deve produrre una scrittura: prima creazione e riallineamento
    /// contavano i giorni in modo diverso (lavorativi contro solari), quindi non coincidevano mai
    /// e ogni pagina di elenco riscriveva tutte le sue scadenze.
    /// </summary>
    [Fact]
    public async Task Una_scadenza_gia_allineata_non_tocca_il_preavviso()
    {
        var ticket = CreaTicket(4, apertura: ProssimoLunedi, scadenza: ProssimoLunedi.AddWorkdays(3));
        ticket.ReminderExpiryStatus = ReminderStatus.Sent;
        _db.SaveChanges();

        await _service.SetTicketStateAsync(new TicketDTO { Id = 4, IdCompany = 1 });

        Assert.Equal(ReminderStatus.Sent, Rileggi(4).ReminderExpiryStatus);
    }

    public void Dispose() => _db.Dispose();
}
