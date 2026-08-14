using System.Security.Claims;
using CRM.Client.Services;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Chi legge la chat di un ticket. La chat contiene la conversazione col cliente, quindi la
/// domanda non e' teorica: ci finiscono prezzi, lamentele e cose dette al telefono.
/// <para>
/// La lista filtra per <b>perimetro aziendale</b> (<c>GetVisibleCompanyIds</c>), la stessa fonte
/// di preventivi, ordini, fatture e trattative: <c>null</c> = vede tutto, e succede solo
/// all'azienda madre; una lista = il proprio albero; lista vuota = niente.
/// </para>
/// <para>
/// Prima il filtro passava da <c>CanAccessOtherCompany()</c>, che e' vero <b>anche per i
/// rivenditori</b>. Il risultato era che a un rivenditore non veniva applicato alcun filtro e
/// leggeva le chat dei clienti altrui: il test che lo dimostrava e' ancora qui sotto, girato al
/// contrario.
/// </para>
/// </summary>
public class TicketChatVisibilityTests
{
    private const int IdAziendaMadre = 1;
    private const int IdRivenditore = 2;
    private const int IdClienteAltrui = 3;
    private const int IdClienteDelRivenditore = 4;

    /// <summary>Messaggio sul ticket del cliente di un altro.</summary>
    private const int IdChatAltrui = 100;

    /// <summary>Messaggio sul ticket di un cliente che il rivenditore segue davvero.</summary>
    private const int IdChatSua = 200;

    private static ApplicationDbContext Db(string nome) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    /// <summary>Tre aziende, un ticket del cliente "altrui" con un messaggio in chat.</summary>
    private static async Task<string> BancoAsync()
    {
        var nome = $"crm-chat-{Guid.NewGuid()}";
        using var db = Db(nome);

        db.Companies.AddRange(
            new Company { Id = IdAziendaMadre, RagioneSociale = "Noi", CompanyType = CompanyTypes.HeadCompany },
            new Company { Id = IdRivenditore, RagioneSociale = "Rivenditore", CompanyType = CompanyTypes.Reseller },
            new Company { Id = IdClienteAltrui, RagioneSociale = "Cliente di un altro", CompanyType = CompanyTypes.Customer },
            // Cliente seguito dal rivenditore: e' l'appartenenza all'albero (IdReseller) a metterlo
            // nel suo perimetro, non il tipo di azienda.
            new Company
            {
                Id = IdClienteDelRivenditore,
                RagioneSociale = "Cliente del rivenditore",
                CompanyType = CompanyTypes.Customer,
                IdReseller = IdRivenditore
            });

        db.Tickets.AddRange(
            new Ticket { Id = 10, IdCompany = IdClienteAltrui, IdType = 1, Description = "Guasto" },
            new Ticket { Id = 20, IdCompany = IdClienteDelRivenditore, IdType = 1, Description = "Guasto" });

        db.TicketChats.AddRange(
            new TicketChat
            {
                Id = IdChatAltrui,
                IdTicket = 10,
                IdUser = "tecnico",
                Date = DateTime.Now,
                Message = "Le confermo il preventivo di 4.500 euro"
            },
            new TicketChat
            {
                Id = IdChatSua,
                IdTicket = 20,
                IdUser = "tecnico",
                Date = DateTime.Now,
                Message = "Passiamo giovedi"
            });

        await db.SaveChangesAsync();
        return nome;
    }

    private static PermitsService Permessi(ApplicationDbContext db, ApplicationUser utente)
    {
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

        userManager.FindByNameAsync(utente.UserName!).Returns(utente);

        var contesto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, utente.UserName!) }, "prova"))
        };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(contesto);

        var roleManager = Substitute.For<RoleManager<IdentityRole>>(
            Substitute.For<IRoleStore<IdentityRole>>(), null, null, null, null);

        return new PermitsService(db, userManager, roleManager, accessor, Substitute.For<IAuthorizationService>());
    }

    private static ApplicationUser Utente(string nome, int idAzienda) =>
        new() { Id = nome, UserName = nome, Email = $"{nome}@test.local", IdCompany = idAzienda };

    // ─── La regola, presa da sola ────────────────────────────────────────────

    [Fact]
    public async Task Per_il_rivenditore_CanAccessOtherCompany_e_vero_ed_e_il_motivo_del_guasto()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var permessi = Permessi(db, Utente("venditore", IdRivenditore));

        // Questo metodo non distingue l'azienda madre da un rivenditore, e usarlo come filtro
        // significava non filtrare affatto. Resta legittimo altrove: quello che non puo' fare e'
        // decidere CHE COSA si vede.
        Assert.True(await permessi.CanAccessOtherCompany());
    }

    [Fact]
    public async Task Il_perimetro_di_un_rivenditore_non_e_tutto()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var permessi = Permessi(db, Utente("venditore", IdRivenditore));

        // null vorrebbe dire "vede tutto": per un rivenditore dev'essere una lista, e il cliente
        // di un altro non ci deve stare dentro.
        var perimetro = await permessi.GetVisibleCompanyIds();

        Assert.NotNull(perimetro);
        Assert.DoesNotContain(IdClienteAltrui, perimetro!);

        // ...ma il cliente che segue davvero c'e', altrimenti il perimetro sarebbe solo un modo
        // elegante di non far vedere niente a nessuno.
        Assert.Contains(IdClienteDelRivenditore, perimetro!);
    }

    [Fact]
    public async Task Il_perimetro_dell_azienda_madre_e_tutto()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var permessi = Permessi(db, Utente("interno", IdAziendaMadre));

        Assert.Null(await permessi.GetVisibleCompanyIds());
    }

    // ─── L'effetto sulla chat ────────────────────────────────────────────────

    /// <param name="aziendeVisibili">null = vede tutto (azienda madre), lista = perimetro.</param>
    private static TicketChatsController Controller(ApplicationDbContext db, List<int>? aziendeVisibili, int idAziendaUtente)
    {
        var utente = Utente("utente", idAziendaUtente);

        var permessi = Substitute.For<IPermitsService>();
        permessi.GetVisibleCompanyIds().Returns(aziendeVisibili);
        permessi.GetIdCompany().Returns(idAziendaUtente);
        permessi.IdUser().Returns(utente.Id);
        permessi.GetUser().Returns(utente);

        return new TicketChatsController(
            db,
            permessi,
            Substitute.For<ILogEventService>(),
            Substitute.For<ITicketChatNotificationService>(),
            Substitute.For<IArchiveService>())
        {
            // Il metodo scrive un'intestazione di paginazione sulla risposta: senza un contesto
            // HTTP fallisce, e fallendo dentro il suo try/catch tornerebbe semplicemente null.
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private static async Task<bool> LeggeIlPrezzoAsync(ApplicationDbContext db, List<int>? aziendeVisibili, int idAziendaUtente)
    {
        var risposta = await Controller(db, aziendeVisibili, idAziendaUtente)
            .GetTicketChats(new TicketChatFilterModel { IdTicket = 10, Skip = 0, Top = 50 });

        Assert.NotNull(risposta);
        return risposta!.Items.Any(m => m.Message != null && m.Message.Contains("4.500"));
    }

    [Fact]
    public async Task Il_rivenditore_non_legge_piu_la_chat_del_cliente_di_un_altro()
    {
        // Il caso da cui e' nata la correzione: il ticket e' del cliente di un altro, il messaggio
        // contiene un prezzo, e prima arrivava lo stesso perche' al rivenditore non veniva
        // applicato alcun filtro.
        var nome = await BancoAsync();
        using var db = Db(nome);

        var perimetroDelRivenditore = new List<int> { IdRivenditore };

        Assert.False(await LeggeIlPrezzoAsync(db, perimetroDelRivenditore, IdRivenditore));
    }

    [Fact]
    public async Task Il_rivenditore_legge_le_chat_dei_clienti_suoi()
    {
        // La correzione non deve chiudergli anche quello che gli spetta: se il cliente e' nel suo
        // albero, la chat la vede.
        var nome = await BancoAsync();
        using var db = Db(nome);

        var perimetroCheComprendeIlCliente = new List<int> { IdRivenditore, IdClienteAltrui };

        Assert.True(await LeggeIlPrezzoAsync(db, perimetroCheComprendeIlCliente, IdRivenditore));
    }

    [Fact]
    public async Task L_azienda_madre_vede_tutte_le_chat()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        // null = nessun perimetro, ed e' il caso della sola azienda madre.
        Assert.True(await LeggeIlPrezzoAsync(db, aziendeVisibili: null, idAziendaUtente: IdAziendaMadre));
    }

    [Fact]
    public async Task Il_cliente_non_vede_le_chat_delle_altre_aziende()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var soloLaPropria = new List<int> { 99 };

        Assert.False(await LeggeIlPrezzoAsync(db, soloLaPropria, 99));
    }

    // ─── Il singolo messaggio, che passa da un'altra regola ──────────────────

    /// <summary>
    /// Controller con i permessi <b>veri</b>: qui non si prova il filtro della lista ma
    /// <c>CanGetTicket</c>, quindi sostituirlo vorrebbe dire non provare niente.
    /// </summary>
    private static TicketChatsController ControllerConPermessiVeri(ApplicationDbContext db, ApplicationUser utente) =>
        new(db,
            Permessi(db, utente),
            Substitute.For<ILogEventService>(),
            Substitute.For<ITicketChatNotificationService>(),
            Substitute.For<IArchiveService>());

    [Fact]
    public async Task Il_singolo_messaggio_di_un_cliente_altrui_e_negato_al_rivenditore()
    {
        // La lista e il singolo messaggio seguono due regole diverse: se questa fosse piu'
        // permissiva, chiudere la lista non servirebbe a niente - basterebbe conoscere l'id.
        var nome = await BancoAsync();
        using var db = Db(nome);

        var risposta = await ControllerConPermessiVeri(db, Utente("venditore", IdRivenditore))
            .GetTicketChat(IdChatAltrui);

        Assert.Null(risposta.Value);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestResult>(risposta.Result);
    }

    [Fact]
    public async Task Il_singolo_messaggio_di_un_cliente_suo_arriva_al_rivenditore()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var risposta = await ControllerConPermessiVeri(db, Utente("venditore", IdRivenditore))
            .GetTicketChat(IdChatSua);

        Assert.NotNull(risposta.Value);
        Assert.Contains("giovedi", risposta.Value!.Message);
    }

    [Fact]
    public async Task Il_singolo_messaggio_arriva_sempre_all_azienda_madre()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var risposta = await ControllerConPermessiVeri(db, Utente("interno", IdAziendaMadre))
            .GetTicketChat(IdChatAltrui);

        Assert.NotNull(risposta.Value);
    }

    [Fact]
    public async Task Il_singolo_messaggio_di_un_altra_azienda_e_negato_al_cliente()
    {
        var nome = await BancoAsync();
        using var db = Db(nome);

        var risposta = await ControllerConPermessiVeri(db, Utente("cliente", IdClienteDelRivenditore))
            .GetTicketChat(IdChatAltrui);

        Assert.Null(risposta.Value);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestResult>(risposta.Result);
    }

    [Fact]
    public async Task Un_perimetro_vuoto_non_apre_niente()
    {
        // Fail-closed: utente non risolvibile o senza azienda. Prima un caso del genere finiva nel
        // ramo "vede tutto", che e' esattamente il rovescio.
        var nome = await BancoAsync();
        using var db = Db(nome);

        Assert.False(await LeggeIlPrezzoAsync(db, new List<int>(), IdRivenditore));
    }
}
