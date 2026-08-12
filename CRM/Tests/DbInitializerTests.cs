using CRM.Server.Data;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Tests;

/// <summary>
/// Dati minimi di un'installazione nuova.
/// <para>
/// Il seeder gira a <b>ogni</b> avvio, quindi la proprieta' che conta non e' "riempie un database
/// vuoto" - quella e' facile - ma "non fa danni su un database pieno". Un'installazione che ha
/// rinominato i propri stati ticket o cambiato i colori se li deve ritrovare identici al riavvio
/// successivo, e non deve accumulare doppioni.
/// </para>
/// </summary>
public class DbInitializerTests
{
    private static (ServiceProvider Servizi, Func<ApplicationDbContext> Nuovo) Banco()
    {
        var nome = $"crm-seed-{Guid.NewGuid()}";

        var servizi = new ServiceCollection();
        servizi.AddLogging();
        servizi.AddDbContext<ApplicationDbContext>(o => o
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

        var provider = servizi.BuildServiceProvider();

        ApplicationDbContext Nuovo() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        return (provider, Nuovo);
    }

    [Fact]
    public async Task Un_database_vuoto_diventa_utilizzabile()
    {
        var (servizi, nuovo) = Banco();

        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();

        // Senza nemmeno una lingua la risoluzione dei nomi tradotti torna null e le descrizioni
        // spariscono senza errori: e' il guasto piu' silenzioso dei quattro.
        Assert.Equal(5, db.Languages.Count());
        Assert.Contains(db.Languages, l => l.LanguageCode == "it-IT");

        // Un valore dell'enum per riga: e' su State che il codice cerca lo stato.
        var stati = db.TicketStates.Select(x => x.State).OrderBy(x => x).ToList();
        Assert.Equal(Enum.GetValues<eTicketStates>().Select(x => (int)x).OrderBy(x => x), stati);

        Assert.Single(db.GlobalSettings);
        Assert.Single(db.TicketTypes);
    }

    [Fact]
    public async Task Girare_due_volte_non_raddoppia_niente()
    {
        var (servizi, nuovo) = Banco();

        await DbInitializer.SeedAsync(servizi);
        await DbInitializer.SeedAsync(servizi);
        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();

        Assert.Equal(5, db.Languages.Count());
        Assert.Equal(5, db.TicketStates.Count());
        Assert.Single(db.GlobalSettings);
        Assert.Single(db.TicketTypes);
    }

    [Fact]
    public async Task Le_etichette_gia_personalizzate_restano_come_sono()
    {
        var (servizi, nuovo) = Banco();

        using (var seme = nuovo())
        {
            seme.TicketStates.Add(new TicketState
            {
                State = (int)eTicketStates.Closed,
                Description = "Risolto e archiviato",
                Color = "#000000"
            });
            await seme.SaveChangesAsync();
        }

        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();

        var chiuso = db.TicketStates.Single(x => x.State == (int)eTicketStates.Closed);

        Assert.Equal("Risolto e archiviato", chiuso.Description);
        Assert.Equal("#000000", chiuso.Color);

        // ...e gli altri quattro sono comunque arrivati.
        Assert.Equal(5, db.TicketStates.Count());
    }

    [Fact]
    public async Task Le_impostazioni_gia_scelte_non_vengono_riscritte()
    {
        var (servizi, nuovo) = Banco();

        using (var seme = nuovo())
        {
            seme.GlobalSettings.Add(new GlobalSetting
            {
                RemoteSignatureEnabled = true,
                SignatureLinkValidityDays = 30
            });
            await seme.SaveChangesAsync();
        }

        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();
        var impostazioni = db.GlobalSettings.Single();

        Assert.True(impostazioni.RemoteSignatureEnabled);
        Assert.Equal(30, impostazioni.SignatureLinkValidityDays);
    }

    [Fact]
    public async Task Dove_i_tipi_ticket_esistono_gia_non_se_ne_aggiungono()
    {
        var (servizi, nuovo) = Banco();

        using (var seme = nuovo())
        {
            seme.TicketTypes.Add(new TicketType { Desc = "Manutenzione impianti" });
            await seme.SaveChangesAsync();
        }

        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();

        var tipo = Assert.Single(db.TicketTypes);
        Assert.Equal("Manutenzione impianti", tipo.Desc);
    }

    [Fact]
    public async Task Una_lingua_gia_presente_non_viene_duplicata_per_differenza_di_maiuscole()
    {
        var (servizi, nuovo) = Banco();

        using (var seme = nuovo())
        {
            seme.Languages.Add(new Language { Name = "Italiano", LanguageCode = "IT-it", Index = 0 });
            await seme.SaveChangesAsync();
        }

        await DbInitializer.SeedAsync(servizi);

        using var db = nuovo();

        Assert.Equal(5, db.Languages.Count());
        Assert.Single(db.Languages, l => string.Equals(l.LanguageCode, "it-IT", StringComparison.OrdinalIgnoreCase));
    }
}
