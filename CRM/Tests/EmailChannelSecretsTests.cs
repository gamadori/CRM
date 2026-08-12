using System.Reflection;
using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Credenziali dei canali email (SMTP in uscita e IMAP in ingresso).
/// <para>
/// Qui si difendono due cose che erano entrambe rotte: il fatto che questi endpoint siano
/// riservati agli amministratori - con il solo login chiunque leggeva password e API key in
/// chiaro, e poteva dirottare la posta dell'azienda su un server proprio - e il fatto che i
/// segreti non tornino mai al client. La seconda regola ne porta con se' una terza, ed e' quella
/// piu' facile da rompere per sbaglio: se la maschera non ha la password, salvare senza toccarla
/// non deve cancellarla.
/// </para>
/// </summary>
public class EmailChannelSecretsTests
{
    private static DbContextOptions<ApplicationDbContext> Opzioni(string nome) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static SmtpSettingsController Smtp(DbContextOptions<ApplicationDbContext> opzioni) =>
        new(new ApplicationDbContext(opzioni), Substitute.For<IHttpClientFactory>());

    private static EmailInboxController Imap(DbContextOptions<ApplicationDbContext> opzioni) =>
        new(new ApplicationDbContext(opzioni));

    [Theory]
    [InlineData(typeof(SmtpSettingsController))]
    [InlineData(typeof(EmailInboxController))]
    public void I_canali_email_restano_riservati_agli_amministratori(Type controller)
    {
        var autorizzazione = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(autorizzazione);
        Assert.Equal("AdminRole", autorizzazione!.Policy);
    }

    // ─── SMTP in uscita ──────────────────────────────────────────────────────

    [Fact]
    public async Task La_password_smtp_non_torna_al_client_ma_si_sa_che_esiste()
    {
        var opzioni = Opzioni($"smtp-lettura-{Guid.NewGuid()}");

        using (var seme = new ApplicationDbContext(opzioni))
        {
            seme.SmtpSettings.Add(new SmtpSetting
            {
                Name = "Primario",
                Server = "smtp.azienda.it",
                Username = "posta@azienda.it",
                Password = "segretissima",
                ApiKey = "chiave-brevo"
            });
            await seme.SaveChangesAsync();
        }

        var risposta = await Smtp(opzioni).GetList();
        var contenuto = Assert.IsType<OkObjectResult>(risposta.Result);
        var canale = Assert.IsType<List<SmtpSetting>>(contenuto.Value).Single();

        Assert.Equal(string.Empty, canale.Password);
        Assert.Null(canale.ApiKey);
        Assert.True(canale.HasPassword);
        Assert.True(canale.HasApiKey);

        // Il resto della configurazione deve restare leggibile: e' la maschera dell'amministratore.
        Assert.Equal("smtp.azienda.it", canale.Server);
        Assert.Equal("posta@azienda.it", canale.Username);
    }

    [Fact]
    public async Task Salvare_senza_toccare_la_password_non_la_cancella()
    {
        var opzioni = Opzioni($"smtp-scrittura-{Guid.NewGuid()}");
        int id;

        using (var seme = new ApplicationDbContext(opzioni))
        {
            var canale = new SmtpSetting
            {
                Name = "Primario",
                Server = "smtp.azienda.it",
                Password = "segretissima",
                ApiKey = "chiave-brevo"
            };
            seme.SmtpSettings.Add(canale);
            await seme.SaveChangesAsync();
            id = canale.Id;
        }

        // Esattamente quello che rimanda la maschera: i segreti non li ha mai avuti.
        var modificato = new SmtpSetting
        {
            Id = id,
            Name = "Primario rinominato",
            Server = "smtp.azienda.it",
            Password = string.Empty,
            ApiKey = null
        };

        var esito = await Smtp(opzioni).PutSmtpSettings(id, modificato);
        Assert.IsType<NoContentResult>(esito);

        using var verifica = new ApplicationDbContext(opzioni);
        var salvato = verifica.SmtpSettings.Single(x => x.Id == id);

        Assert.Equal("segretissima", salvato.Password);
        Assert.Equal("chiave-brevo", salvato.ApiKey);
        Assert.Equal("Primario rinominato", salvato.Name);
    }

    [Fact]
    public async Task Una_password_nuova_sostituisce_quella_salvata()
    {
        var opzioni = Opzioni($"smtp-cambio-{Guid.NewGuid()}");
        int id;

        using (var seme = new ApplicationDbContext(opzioni))
        {
            var canale = new SmtpSetting { Name = "Primario", Password = "vecchia" };
            seme.SmtpSettings.Add(canale);
            await seme.SaveChangesAsync();
            id = canale.Id;
        }

        await Smtp(opzioni).PutSmtpSettings(id, new SmtpSetting { Id = id, Name = "Primario", Password = "nuova" });

        using var verifica = new ApplicationDbContext(opzioni);
        Assert.Equal("nuova", verifica.SmtpSettings.Single(x => x.Id == id).Password);
    }

    [Fact]
    public async Task Nemmeno_l_eco_della_creazione_restituisce_i_segreti()
    {
        var opzioni = Opzioni($"smtp-creazione-{Guid.NewGuid()}");

        var risposta = await Smtp(opzioni).PostSmtpSettings(new SmtpSetting
        {
            Name = "Nuovo",
            Password = "segretissima"
        });

        var contenuto = Assert.IsType<OkObjectResult>(risposta.Result);
        var creato = Assert.IsType<SmtpSetting>(contenuto.Value);

        Assert.Equal(string.Empty, creato.Password);
        Assert.True(creato.HasPassword);
        Assert.True(creato.Id > 0);

        // ...ma sul database la password c'e', altrimenti avremmo salvato un canale inservibile.
        using var verifica = new ApplicationDbContext(opzioni);
        Assert.Equal("segretissima", verifica.SmtpSettings.Single().Password);
    }

    // ─── IMAP in ingresso ────────────────────────────────────────────────────

    [Fact]
    public async Task La_password_imap_non_torna_al_client_ma_il_token_webhook_si()
    {
        var opzioni = Opzioni($"imap-lettura-{Guid.NewGuid()}");

        using (var seme = new ApplicationDbContext(opzioni))
        {
            seme.EmailInboxes.Add(new EmailInbox
            {
                Name = "Assistenza",
                Address = "assistenza@azienda.it",
                Host = "imap.azienda.it",
                Password = "segretissima",
                WebhookToken = "token-da-incollare-nel-provider"
            });
            await seme.SaveChangesAsync();
        }

        var risposta = await Imap(opzioni).GetList();
        var contenuto = Assert.IsType<OkObjectResult>(risposta.Result);
        var casella = Assert.IsType<List<EmailInbox>>(contenuto.Value).Single();

        Assert.Null(casella.Password);
        Assert.True(casella.HasPassword);

        // Il token webhook e' fatto per essere copiato nella configurazione del provider: se lo
        // nascondessimo, la casella inbound-parse non sarebbe piu' configurabile da nessuno.
        Assert.Equal("token-da-incollare-nel-provider", casella.WebhookToken);
    }

    [Fact]
    public async Task Salvare_la_casella_senza_password_non_la_cancella()
    {
        var opzioni = Opzioni($"imap-scrittura-{Guid.NewGuid()}");
        int id;

        using (var seme = new ApplicationDbContext(opzioni))
        {
            var casella = new EmailInbox
            {
                Name = "Assistenza",
                Address = "assistenza@azienda.it",
                Password = "segretissima"
            };
            seme.EmailInboxes.Add(casella);
            await seme.SaveChangesAsync();
            id = casella.Id;
        }

        var esito = await Imap(opzioni).Put(id, new EmailInbox
        {
            Id = id,
            Name = "Assistenza",
            Address = "assistenza@azienda.it",
            PollingSeconds = 300,
            Password = null
        });

        Assert.IsType<NoContentResult>(esito);

        using var verifica = new ApplicationDbContext(opzioni);
        var salvata = verifica.EmailInboxes.Single(x => x.Id == id);

        Assert.Equal("segretissima", salvata.Password);
        Assert.Equal(300, salvata.PollingSeconds);
    }
}
