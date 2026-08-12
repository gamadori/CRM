using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using static CRM.Shared.LogEvent;

namespace CRM.Tests;

/// <summary>
/// Scrittura del log applicativo. E' il servizio piu' chiamato del server (centinaia di punti,
/// quasi tutti dentro un <c>catch</c>) ed era anche quello che faceva sparire gli errori: usando
/// il DbContext condiviso del chiamante, la scrittura del log ritentava le entita' rimaste
/// tracciate dalla SaveChanges appena fallita e rilanciava, sostituendo l'eccezione originale.
/// <para>
/// Qui si difendono le due proprieta' che lo rendono innocuo: scrive solo la propria riga, e non
/// fallisce mai verso chi lo ha chiamato.
/// </para>
/// </summary>
public class LogEventIsolationTests
{
    /// <summary>
    /// Banco di prova: una base dati in memoria, il contesto del chiamante e il servizio di log
    /// che ne apre uno suo attraverso la fabbrica di scope - come accade in una richiesta vera.
    /// </summary>
    private sealed class Banco : IDisposable
    {
        private readonly string _nome = $"crm-log-{Guid.NewGuid()}";
        private readonly ServiceProvider _radice;

        public ApplicationDbContext Chiamante { get; }
        public LogEventService Servizio { get; }

        public Banco()
        {
            var servizi = new ServiceCollection();
            servizi.AddDbContext<ApplicationDbContext>(o => Configura(o, _nome));
            _radice = servizi.BuildServiceProvider();

            Chiamante = NuovoContesto();

            Servizio = new LogEventService(
                Chiamante,
                Substitute.For<IHttpContextAccessor>(),
                _radice.GetRequiredService<IServiceScopeFactory>(),
                Substitute.For<ILogger<LogEventService>>());
        }

        public ApplicationDbContext NuovoContesto()
        {
            var opzioni = new DbContextOptionsBuilder<ApplicationDbContext>();
            Configura(opzioni, _nome);
            return new ApplicationDbContext(opzioni.Options);
        }

        private static void Configura(DbContextOptionsBuilder opzioni, string nome) => opzioni
            .UseInMemoryDatabase(nome)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));

        public void Dispose()
        {
            Chiamante.Dispose();
            _radice.Dispose();
        }
    }

    /// <summary>Servizio con una fabbrica di scope rotta: il database non si raggiunge.</summary>
    private static LogEventService ConDatabaseIrraggiungibile()
    {
        var fabbrica = Substitute.For<IServiceScopeFactory>();
        fabbrica.CreateScope().Returns(_ => throw new InvalidOperationException("database irraggiungibile"));

        return new LogEventService(
            null!,
            Substitute.For<IHttpContextAccessor>(),
            fabbrica,
            Substitute.For<ILogger<LogEventService>>());
    }

    [Fact]
    public async Task Il_log_non_salva_le_modifiche_rimaste_in_sospeso_nel_chiamante()
    {
        using var banco = new Banco();

        // Il chiamante ha una modifica tracciata e NON salvata: e' esattamente lo stato in cui si
        // trova un servizio dentro il catch, dopo una SaveChanges fallita.
        banco.Chiamante.SmtpSettings.Add(new SmtpSetting { Name = "Roba mai salvata" });

        await banco.Servizio.RegisterAsync("Modulo", "Rotina", EventsTypes.Error, "qualcosa e' andato storto");

        using var verifica = banco.NuovoContesto();

        // La riga di log c'e'...
        var registrato = verifica.LogEvents.Single();
        Assert.Equal("Modulo", registrato.Module);
        Assert.Equal(EventsTypes.Error, registrato.EventType);

        // ...e la modifica in sospeso del chiamante e' rimasta dov'era.
        Assert.Empty(verifica.SmtpSettings);
    }

    [Fact]
    public async Task L_eccezione_passata_al_log_finisce_nel_messaggio()
    {
        using var banco = new Banco();

        await banco.Servizio.RegisterAsync("Modulo", "Rotina", EventsTypes.Error,
            new InvalidOperationException("il salvataggio e' fallito"));

        using var verifica = banco.NuovoContesto();

        Assert.Contains("il salvataggio e' fallito", verifica.LogEvents.Single().Message);
    }

    [Fact]
    public async Task Se_il_log_non_si_puo_scrivere_l_errore_vero_arriva_comunque_al_chiamante()
    {
        // Se RegisterAsync rilanciasse, sostituirebbe l'eccezione che il chiamante sta gestendo:
        // e' precisamente il difetto da cui nasce questo test.
        var eccezione = await Record.ExceptionAsync(() =>
            ConDatabaseIrraggiungibile().RegisterAsync("Modulo", "Rotina", EventsTypes.Error, "messaggio"));

        Assert.Null(eccezione);
    }

    [Fact]
    public void Vale_anche_per_la_versione_sincrona()
    {
        var eccezione = Record.Exception(() =>
            ConDatabaseIrraggiungibile().Register("Modulo", "Rotina", EventsTypes.Error, "messaggio"));

        Assert.Null(eccezione);
    }
}
