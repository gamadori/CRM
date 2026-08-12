using CRM.Server.Data;
using CRM.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Tests;

/// <summary>
/// Cifratura dei segreti sul database (password SMTP/IMAP, chiavi dei provider, token dei webhook).
/// <para>
/// Serve contro un backup rubato: chi legge le righe non deve trovarci delle credenziali. Le tre
/// cose che possono andare storte, e che i test tengono ferme, sono: il valore deve uscire cifrato
/// <b>davvero</b> dal convertitore; le righe scritte prima della cifratura devono continuare a
/// leggersi (altrimenti il giorno del rilascio smette di funzionare la posta); e con chiavi
/// sbagliate un segreto deve risultare <b>assente</b>, non spacciato per buono.
/// </para>
/// </summary>
public class SecretProtectionTests
{
    private static ISecretProtector Cifratore(string nomeChiave = "prova")
        => new DataProtectionSecretProtector(DataProtectionProvider.Create(nomeChiave));

    // ─── Il cifratore ────────────────────────────────────────────────────────

    [Fact]
    public void Un_segreto_cifrato_torna_uguale_a_se_stesso()
    {
        var cifratore = Cifratore();

        var cifrato = cifratore.Protect("segretissima");

        Assert.NotEqual("segretissima", cifrato);
        Assert.StartsWith("enc:v1:", cifrato);
        Assert.Equal("segretissima", cifratore.Unprotect(cifrato));
    }

    [Fact]
    public void Cifrare_due_volte_non_raddoppia_la_cifratura()
    {
        var cifratore = Cifratore();

        var una = cifratore.Protect("segretissima");
        var due = cifratore.Protect(una);

        Assert.Equal(una, due);
        Assert.Equal("segretissima", cifratore.Unprotect(due));
    }

    [Fact]
    public void Una_riga_scritta_prima_della_cifratura_si_legge_lo_stesso()
    {
        // E' il caso del giorno del rilascio: sul database ci sono ancora i valori in chiaro.
        // Se qui si rispondesse con un errore o con una stringa vuota, la posta smetterebbe di
        // partire nel momento esatto in cui si aggiorna.
        var cifratore = Cifratore();

        Assert.Equal("vecchia-in-chiaro", cifratore.Unprotect("vecchia-in-chiaro"));
    }

    [Fact]
    public void Con_le_chiavi_sbagliate_il_segreto_risulta_assente_non_sbagliato()
    {
        var cifrato = Cifratore("chiavi-di-ieri").Protect("segretissima");

        var letto = Cifratore("chiavi-di-oggi").Unprotect(cifrato);

        // Vuoto, non il testo cifrato: la maschera lo mostrera' come "nessuna password salvata" e
        // l'amministratore la reinserisce. Restituire il testo cifrato significherebbe provare a
        // fare login sul server di posta con una stringa a caso, e un errore che non c'entra.
        Assert.Equal(string.Empty, letto);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Il_vuoto_resta_vuoto(string? valore)
    {
        var cifratore = Cifratore();

        Assert.Equal(valore, cifratore.Protect(valore!));
        Assert.Equal(valore, cifratore.Unprotect(valore!));
    }

    // ─── L'aggancio a EF ─────────────────────────────────────────────────────

    private static DbContextOptions<ApplicationDbContext> Opzioni(string nome)
        => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .ReplaceService<IModelCacheKeyFactory, SecretAwareModelCacheKeyFactory>()
            .Options;

    [Theory]
    [InlineData(typeof(SmtpSetting), nameof(SmtpSetting.Password))]
    [InlineData(typeof(SmtpSetting), nameof(SmtpSetting.ApiKey))]
    [InlineData(typeof(EmailInbox), nameof(EmailInbox.Password))]
    [InlineData(typeof(EmailInbox), nameof(EmailInbox.WebhookToken))]
    public void I_quattro_segreti_escono_cifrati_dal_convertitore(Type entita, string proprieta)
    {
        using var db = new ApplicationDbContext(Opzioni($"crm-sec-{Guid.NewGuid()}"), Cifratore());

        var convertitore = db.Model
            .FindEntityType(entita)!
            .FindProperty(proprieta)!
            .GetValueConverter();

        Assert.NotNull(convertitore);

        var scritto = (string)convertitore!.ConvertToProvider("segretissima")!;

        Assert.StartsWith("enc:v1:", scritto);
        Assert.Equal("segretissima", convertitore.ConvertFromProvider(scritto));
    }

    [Fact]
    public void Un_contesto_cifrante_e_uno_no_non_si_scambiano_il_modello()
    {
        // Il modello di EF e' in cache per tipo di contesto: senza SecretAwareModelCacheKeyFactory
        // vincerebbe il primo costruito, e un contesto di produzione potrebbe ritrovarsi il
        // modello in chiaro di un contesto di servizio, scrivendo le password senza cifrarle.
        var opzioni = Opzioni($"crm-sec-{Guid.NewGuid()}");

        using var grezzo = new ApplicationDbContext(opzioni);
        using var cifrante = new ApplicationDbContext(opzioni, Cifratore());

        string Converti(ApplicationDbContext db) => (string)db.Model
            .FindEntityType(typeof(SmtpSetting))!
            .FindProperty(nameof(SmtpSetting.Password))!
            .GetValueConverter()!
            .ConvertToProvider("segretissima")!;

        Assert.Equal("segretissima", Converti(grezzo));
        Assert.StartsWith("enc:v1:", Converti(cifrante));
    }

    // ─── La conversione delle righe vecchie ──────────────────────────────────

    private static ServiceProvider Servizi(DbContextOptions<ApplicationDbContext> opzioni, ISecretProtector cifratore)
    {
        var servizi = new ServiceCollection();
        servizi.AddLogging();
        servizi.AddSingleton(opzioni);
        servizi.AddSingleton(cifratore);

        return servizi.BuildServiceProvider();
    }

    [Fact]
    public async Task Le_righe_in_chiaro_vengono_cifrate_all_avvio()
    {
        var opzioni = Opzioni($"crm-sec-{Guid.NewGuid()}");
        var cifratore = Cifratore();

        using (var seme = new ApplicationDbContext(opzioni))
        {
            seme.SmtpSettings.Add(new SmtpSetting { Name = "Primario", Password = "in-chiaro", ApiKey = "chiave-in-chiaro" });
            seme.EmailInboxes.Add(new EmailInbox { Name = "Assistenza", Password = "imap-in-chiaro", WebhookToken = "token-in-chiaro" });
            await seme.SaveChangesAsync();
        }

        await SecretsProtectionStartup.RunAsync(Servizi(opzioni, cifratore));

        // Letto grezzo: sul database ora c'e' del testo cifrato.
        using var grezzo = new ApplicationDbContext(opzioni);
        var canale = grezzo.SmtpSettings.Single();
        var casella = grezzo.EmailInboxes.Single();

        Assert.StartsWith("enc:v1:", canale.Password);
        Assert.StartsWith("enc:v1:", canale.ApiKey!);
        Assert.StartsWith("enc:v1:", casella.Password!);
        Assert.StartsWith("enc:v1:", casella.WebhookToken!);

        // ...e resta leggibile.
        Assert.Equal("in-chiaro", cifratore.Unprotect(canale.Password));
        Assert.Equal("token-in-chiaro", cifratore.Unprotect(casella.WebhookToken!));
    }

    [Fact]
    public async Task Un_secondo_avvio_non_ricifra_niente()
    {
        var opzioni = Opzioni($"crm-sec-{Guid.NewGuid()}");
        var cifratore = Cifratore();

        using (var seme = new ApplicationDbContext(opzioni))
        {
            seme.SmtpSettings.Add(new SmtpSetting { Name = "Primario", Password = "in-chiaro" });
            await seme.SaveChangesAsync();
        }

        var servizi = Servizi(opzioni, cifratore);

        await SecretsProtectionStartup.RunAsync(servizi);

        string DopoIlPrimoGiro()
        {
            using var db = new ApplicationDbContext(opzioni);
            return db.SmtpSettings.Single().Password;
        }

        var primo = DopoIlPrimoGiro();

        await SecretsProtectionStartup.RunAsync(servizi);

        // Stesso testo cifrato, non uno nuovo: il secondo giro non ha toccato la riga.
        Assert.Equal(primo, DopoIlPrimoGiro());
    }

    [Fact]
    public async Task Un_segreto_illeggibile_non_viene_riscritto()
    {
        // Cartella di chiavi sbagliata: il valore non si decifra. Riscriverlo cifrandolo di nuovo
        // (a partire dalla stringa vuota che si legge) cancellerebbe l'unica copia rimasta, e con
        // le chiavi giuste tornerebbe leggibile.
        var opzioni = Opzioni($"crm-sec-{Guid.NewGuid()}");
        var cifratoIeri = Cifratore("chiavi-di-ieri").Protect("segretissima");

        using (var seme = new ApplicationDbContext(opzioni))
        {
            seme.SmtpSettings.Add(new SmtpSetting { Name = "Primario", Password = cifratoIeri });
            await seme.SaveChangesAsync();
        }

        await SecretsProtectionStartup.RunAsync(Servizi(opzioni, Cifratore("chiavi-di-oggi")));

        using var grezzo = new ApplicationDbContext(opzioni);

        Assert.Equal(cifratoIeri, grezzo.SmtpSettings.Single().Password);
    }
}
