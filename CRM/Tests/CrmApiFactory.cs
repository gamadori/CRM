using System.Security.Claims;
using System.Text.Encodings.Web;
using CRM.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRM.Tests;

/// <summary>
/// Monta l'applicazione vera - pipeline, attributi, criteri di autorizzazione - su un database in
/// memoria, per poter chiedere "questo utente puo' chiamare questo endpoint?" e ricevere la
/// risposta che riceverebbe in produzione.
/// <para>
/// L'<b>autenticazione</b> e' sostituita da uno schema di prova che legge il ruolo da
/// un'intestazione. Non e' una scorciatoia sospetta: qui non si prova come si ottiene un token -
/// quello lo fa OpenIddict - ma cosa succede <b>dopo</b>, e cioe' se il perimetro tiene per un
/// utente con un certo ruolo. Sostituire il rilascio del token toglierebbe di mezzo proprio la
/// parte che si vuole misurare; sostituire il modo di presentarlo no.
/// </para>
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    public const string HeaderRuolo = "X-Test-Role";
    public const string SchemaDiProva = "Prova";

    private readonly string _nomeDatabase = $"crm-api-{Guid.NewGuid()}";
    private readonly string _cartellaChiavi =
        Path.Combine(Path.GetTempPath(), "crm-test-keys", Guid.NewGuid().ToString("N"));

    public CrmApiFactory()
    {
        // Il percorso delle chiavi serve PRIMA che l'applicazione sia costruita (senza, si rifiuta
        // di partire) e le impostazioni passate al builder differito arrivano troppo tardi. Le
        // variabili d'ambiente invece le legge sempre, perche' fanno parte della configurazione di
        // partenza. Il doppio trattino basso e' il modo di scrivere le sezioni annidate.
        Environment.SetEnvironmentVariable("DataProtection__KeysPath", _cartellaChiavi);
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection", "Server=(prova);Database=prova;Trusted_Connection=True;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: e' l'unico ambiente in cui OpenIddict si genera da solo i certificati,
        // invece di pretendere quello indicato in configurazione.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            SostituisciDatabase(services);
            TogliServiziInSottofondo(services);
            SostituisciAutenticazione(services);
        });
    }

    /// <summary>
    /// Database in memoria al posto di SQL Server, con la stessa configurazione del vero.
    /// <para>
    /// Non basta togliere <c>DbContextOptions&lt;T&gt;</c>: da EF 10 la configurazione del provider
    /// viaggia anche in <c>IDbContextOptionsConfiguration&lt;T&gt;</c>, e lasciandola indietro si
    /// finisce con due provider registrati insieme - EF si rifiuta di partire, dicendolo peraltro
    /// molto chiaramente.
    /// </para>
    /// </summary>
    private void SostituisciDatabase(IServiceCollection services)
    {
        var daTogliere = services
            .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                     || d.ServiceType == typeof(DbContextOptions)
                     || d.ServiceType == typeof(ApplicationDbContext)
                     || d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))
            .ToList();

        foreach (var descrittore in daTogliere)
            services.Remove(descrittore);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase(_nomeDatabase);
            options.UseOpenIddict();
            options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            options.ReplaceService<IModelCacheKeyFactory, SecretAwareModelCacheKeyFactory>();
        });
    }

    /// <summary>
    /// Via i sei servizi in sottofondo: manderebbero email, leggerebbero caselle IMAP e
    /// scriverebbero promemoria mentre la suite gira.
    /// </summary>
    private static void TogliServiziInSottofondo(IServiceCollection services)
    {
        foreach (var descrittore in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
            services.Remove(descrittore);
    }

    private static void SostituisciAutenticazione(IServiceCollection services)
    {
        services.AddAuthentication(SchemaDiProva)
            .AddScheme<AuthenticationSchemeOptions, RuoloDaIntestazioneHandler>(SchemaDiProva, _ => { });

        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = SchemaDiProva;
            options.DefaultAuthenticateScheme = SchemaDiProva;
            options.DefaultChallengeScheme = SchemaDiProva;
        });
    }

    /// <summary>Client che si presenta con un ruolo; senza ruolo, come un utente non autenticato.</summary>
    public HttpClient ClientCome(string? ruolo)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        if (ruolo != null)
            client.DefaultRequestHeaders.Add(HeaderRuolo, ruolo);

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            Environment.SetEnvironmentVariable("DataProtection__KeysPath", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        }

        if (disposing && Directory.Exists(_cartellaChiavi))
        {
            try { Directory.Delete(_cartellaChiavi, recursive: true); } catch { /* cartella usa e getta */ }
        }
    }

    /// <summary>
    /// Autentica in base all'intestazione <c>X-Test-Role</c>. Senza intestazione non autentica
    /// affatto, cosi' si puo' provare anche il caso "nessun login".
    /// </summary>
    private sealed class RuoloDaIntestazioneHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public RuoloDaIntestazioneHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderRuolo, out var ruolo) || string.IsNullOrWhiteSpace(ruolo))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, $"utente-{ruolo}"),
                new Claim(ClaimTypes.Name, $"utente-{ruolo}"),
                new Claim(ClaimTypes.Role, ruolo.ToString())
            };

            // I tipi di claim passati al costruttore sono quelli che IsInRole andra' a leggere:
            // sbagliarli farebbe fallire ogni RequireRole e i test sarebbero verdi per il motivo
            // sbagliato (tutto negato).
            var identita = new ClaimsIdentity(claims, SchemaDiProva, ClaimTypes.Name, ClaimTypes.Role);
            var biglietto = new AuthenticationTicket(new ClaimsPrincipal(identita), SchemaDiProva);

            return Task.FromResult(AuthenticateResult.Success(biglietto));
        }
    }
}
