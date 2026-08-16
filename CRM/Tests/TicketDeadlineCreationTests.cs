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
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// La scadenza nel momento in cui il ticket NASCE. Il riallineamento successivo e' presidiato da
/// <see cref="TicketExpiryRefreshTests"/>: qui si guarda la data che il ticket ha addosso appena
/// creato, prima che qualcuno apra un elenco.
/// <para>
/// Conta perche' su quella data viene programmato il preavviso, e perche' un ticket puo' restare
/// giorni senza che nessuno apra l'elenco che lo correggerebbe.
/// </para>
/// <para>
/// I due difetti presidiati. Dentro il CRM i giorni concessi si cercavano per <b>id di ticket</b>
/// su un ticket non ancora salvato, quindi con id 0: nessuna riga corrispondeva e valeva per tutti
/// il ripiego fisso di 3 giorni, qualunque cosa dicesse il tipo. Dalle API esterne i giorni si
/// contavano <b>solari</b> invece che lavorativi, quindi la scadenza nasceva diversa da quella che
/// il riallineamento avrebbe scritto un minuto dopo.
/// </para>
/// </summary>
public class TicketDeadlineCreationTests : IDisposable
{
    private const int IdTypeVeloce = 9;
    private const int IdCompany = 1;

    private readonly ApplicationDbContext _db;
    private readonly TicketsService _service;

    public TicketDeadlineCreationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-deadline-creation-{Guid.NewGuid()}")
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

    /// <summary>Venerdi' 6 marzo 2026: marzo non ha festivi italiani, quindi l'unica variabile e'
    /// il fine settimana e il conteggio si puo' affermare. Il lunedi' dopo e' il 9.</summary>
    private static readonly DateTime Venerdi = new(2026, 3, 6);

    private static readonly DateTime Lunedi = new(2026, 3, 9);

    private Ticket TicketNuovo(DateTime data) => new()
    {
        IdCompany = IdCompany,
        IdType = IdTypeVeloce,
        Date = data,
        Description = "Non parte",
        Numero = string.Empty,
        CloseDescription = string.Empty,
        CloseNote = string.Empty
    };

    /// <summary>
    /// Il difetto: il tipo diceva 1 giorno e il ticket nasceva con 3. Non si vedeva a lungo, perche'
    /// la prima apertura di elenco correggeva - ma il preavviso era gia' stato programmato sulla
    /// data sbagliata, e per tre giorni il ticket non risultava in ritardo mentre lo era.
    /// </summary>
    [Fact]
    public async Task Un_ticket_nuovo_prende_i_giorni_del_suo_tipo()
    {
        _db.TicketTypes.Add(new TicketType { Id = IdTypeVeloce, Desc = "Urgenza", ExpiredDate = 1 });
        _db.SaveChanges();

        var salvato = await _service.PostAsync(TicketNuovo(Venerdi));

        Assert.Equal(Lunedi, salvato.DateExpired);
    }

    /// <summary>
    /// Il tipo che non dichiara giorni ripiega sull'impostazione generale, non sul 3 fisso.
    /// Senza questa prova il test qui sopra passerebbe anche con un ripiego sbagliato.
    /// </summary>
    [Fact]
    public async Task Un_tipo_senza_giorni_ripiega_sull_impostazione_generale()
    {
        _db.TicketTypes.Add(new TicketType { Id = IdTypeVeloce, Desc = "Generico", ExpiredDate = 0 });
        _db.GlobalSettings.Add(new GlobalSetting { Id = 1, TicketDaysExpired = 5 });
        _db.SaveChanges();

        var salvato = await _service.PostAsync(TicketNuovo(Venerdi));

        // 5 giorni lavorativi da venerdi' 6: lun 9, mar 10, mer 11, gio 12, ven 13.
        Assert.Equal(new DateTime(2026, 3, 13), salvato.DateExpired);
    }

    /// <summary>
    /// La scadenza calcolata alla nascita deve essere la stessa che il riallineamento scriverebbe
    /// subito dopo. Se le due formule divergono, ogni apertura di elenco sposta la data e rimette
    /// in coda il preavviso: rumore su tutti i ticket, per sempre.
    /// </summary>
    [Fact]
    public async Task La_scadenza_alla_nascita_e_quella_del_riallineamento_coincidono()
    {
        _db.TicketTypes.Add(new TicketType { Id = IdTypeVeloce, Desc = "Urgenza", ExpiredDate = 1 });
        _db.SaveChanges();

        var salvato = await _service.PostAsync(TicketNuovo(Venerdi));
        var allaNascita = salvato.DateExpired;

        await _service.SetTicketStateAsync(new TicketDTO { Id = salvato.Id, IdCompany = IdCompany });

        _db.ChangeTracker.Clear();
        var dopoIlRiallineamento = _db.Tickets.AsNoTracking().Single(t => t.Id == salvato.Id).DateExpired;

        Assert.Equal(allaNascita, dopoIlRiallineamento);
    }

    /// <summary>
    /// Ticket aperto dalle API esterne: stessi giorni lavorativi del resto del CRM. Contandoli
    /// solari, un ticket aperto venerdi' con un giorno di tempo scadeva di sabato.
    /// <para>
    /// La scadenza la decide sempre il CRM: dalla richiesta non arriva piu', e il chiamante puo'
    /// solo leggerla nella risposta.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Un_ticket_aperto_dalle_API_esterne_conta_giorni_lavorativi()
    {
        _db.TicketTypes.Add(new TicketType { Id = IdTypeVeloce, Desc = "Urgenza", ExpiredDate = 1 });
        // La ditta serve davvero: la lettura del ticket appena creato la richiede (la Include su
        // una relazione obbligatoria e' una join che, senza ditta, farebbe sparire la riga).
        _db.Companies.Add(new Company { Id = IdCompany, RagioneSociale = "Cliente test" });
        _db.Users.Add(new ApplicationUser { Id = "tecnico", UserName = "tecnico", Email = "tecnico@test.local" });
        _db.SaveChanges();

        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        userManager.GetUsersInRoleAsync(Arg.Any<string>()).Returns(new List<ApplicationUser>());

        var service = new ExternalTicketApiService(
            _db,
            userManager,
            Substitute.For<IConfiguration>());

        var risposta = await service.CreateTicketAsync(
            new ApiKey { Id = 1, Name = "Portale", Scope = ApiKeyScope.ExternalTicket, IdCompany = IdCompany },
            new ExternalTicketCreateRequest
            {
                IdType = IdTypeVeloce,
                Description = "Non parte",
                Date = Venerdi
            });

        Assert.Equal(Lunedi, risposta.DateExpired);
    }

    public void Dispose() => _db.Dispose();
}
