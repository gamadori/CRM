using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CRM.Tests;

/// <summary>
/// La fotografia che una macchina manda di se stessa: componenti con le loro versioni e
/// totalizzatori di ore e produzione.
/// <para>
/// Il punto del disegno: gli eventi di cambio versione <b>non li dichiara la macchina</b>, li
/// deduce il CRM confrontando due fotografie. Se li mandasse la macchina, un aggiornamento fatto
/// mentre la rete era giu' resterebbe ignoto per sempre e in archivio resterebbe una versione che
/// non esiste piu'. Questi test sono la prova che il confronto regge anche quando qualche
/// collegamento si perde.
/// </para>
/// </summary>
public class MachineStatusTests : IDisposable
{
    private const int IdArticle = 10;

    private readonly ApplicationDbContext _db;
    private readonly MachineStatusService _service;
    private readonly Article _macchina;

    public MachineStatusTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-status-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Companies.Add(new Company { Id = 1, RagioneSociale = "Cliente" });
        _db.Products.Add(new Product { Id = 5, Name = "Linea", Code = "LN" });
        _macchina = new Article { Id = IdArticle, IdCompany = 1, IdProduct = 5, SerialNumber = "2884", Name = "Linea 1" };
        _db.Articles.Add(_macchina);
        _db.SaveChanges();

        _service = new MachineStatusService(_db);
    }

    private Task<MachineStatusResponse> Manda(MachineStatusRequest richiesta)
        => _service.ApplyAsync(_macchina, richiesta, "api-key:1");

    private static MachineStatusRequest Componenti(params (string Code, MachineComponentKind Kind, string? Version)[] items)
        => new()
        {
            Components = items.Select(x => new MachineComponentStatus
            {
                Code = x.Code,
                Kind = x.Kind,
                Version = x.Version
            }).ToList()
        };

    private static MachineStatusRequest Contatori(DateTime giorno, decimal? ore, long? pezzi)
        => new()
        {
            Counters = new MachineCountersStatus { Day = giorno, TotalHours = ore, TotalPieces = pezzi }
        };

    private List<MachineComponentVersionChange> Cambi()
        => _db.MachineComponentVersionChanges.AsNoTracking().OrderBy(x => x.Id).ToList();

    // ─── Componenti e versioni ──────────────────────────────────────────────

    /// <summary>Una linea ha piu' PLC, piu' schede e piu' HMI: devono stare tutti.</summary>
    [Fact]
    public async Task Una_linea_registra_tutti_i_suoi_componenti()
    {
        var esito = await Manda(Componenti(
            ("PLC-1", MachineComponentKind.Plc, "2.14"),
            ("PLC-2", MachineComponentKind.Plc, "2.14"),
            ("HMI-ingresso", MachineComponentKind.Hmi, "5.0"),
            ("HMI-uscita", MachineComponentKind.Hmi, "5.0"),
            ("SCHEDA-taglio", MachineComponentKind.Board, "1.3")));

        Assert.Equal(5, esito.ComponentsReceived);
        Assert.Equal(5, _db.MachineComponents.Count(x => x.IdArticle == IdArticle));
    }

    /// <summary>
    /// La prima versione vista e' gia' un evento: senza, la storia di un componente comincerebbe
    /// dal suo primo aggiornamento e non da com'era quando e' arrivato.
    /// </summary>
    [Fact]
    public async Task La_prima_versione_vista_e_gia_un_evento()
    {
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));

        var cambio = Assert.Single(Cambi());
        Assert.Null(cambio.FromVersion);
        Assert.Equal("2.14", cambio.ToVersion);
    }

    [Fact]
    public async Task Un_cambio_di_versione_viene_registrato_da_solo()
    {
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));
        var esito = await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.15")));

        var cambio = Assert.Single(esito.VersionChanges);
        Assert.Equal("2.14", cambio.FromVersion);
        Assert.Equal("2.15", cambio.ToVersion);
        Assert.Equal("2.15", _db.MachineComponents.Single(x => x.Code == "PLC-1").CurrentVersion);
    }

    /// <summary>
    /// Contro-prova: la stessa fotografia mandata ogni giorno non deve riempire lo storico di
    /// eventi in cui non e' cambiato niente.
    /// </summary>
    [Fact]
    public async Task La_stessa_versione_ripetuta_non_e_un_evento()
    {
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));

        Assert.Single(Cambi());
    }

    /// <summary>
    /// Il motivo per cui la macchina manda una fotografia invece degli eventi: se l'aggiornamento
    /// avviene mentre la rete e' giu', il CRM se ne accorge lo stesso al collegamento dopo.
    /// </summary>
    [Fact]
    public async Task Un_aggiornamento_fatto_offline_viene_recuperato_al_collegamento_dopo()
    {
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));

        // La macchina passa dalla 2.15 alla 2.16 senza riuscire a parlare: nessun evento inviato.
        var esito = await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.16")));

        var cambio = Assert.Single(esito.VersionChanges);
        Assert.Equal("2.14", cambio.FromVersion);
        Assert.Equal("2.16", cambio.ToVersion);
    }

    /// <summary>
    /// Versione assente nella fotografia: la macchina non l'ha detta, non vuol dire che il
    /// componente l'abbia persa. Cancellarla sarebbe inventare un cambio.
    /// </summary>
    [Fact]
    public async Task Una_versione_non_dichiarata_non_cancella_quella_che_c_era()
    {
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, "2.14")));
        await Manda(Componenti(("PLC-1", MachineComponentKind.Plc, null)));

        Assert.Equal("2.14", _db.MachineComponents.Single(x => x.Code == "PLC-1").CurrentVersion);
        Assert.Single(Cambi());
    }

    /// <summary>
    /// La posizione e' l'identita': sostituendo il PLC guasto, la storia delle versioni di quel
    /// punto della linea continua invece di ricominciare.
    /// </summary>
    [Fact]
    public async Task Sostituire_il_pezzo_non_spezza_la_storia_della_posizione()
    {
        await Manda(new MachineStatusRequest
        {
            Components = new()
            {
                new() { Code = "PLC-1", Kind = MachineComponentKind.Plc, Version = "2.14", SerialNumber = "SN-AAA" }
            }
        });

        await Manda(new MachineStatusRequest
        {
            Components = new()
            {
                new() { Code = "PLC-1", Kind = MachineComponentKind.Plc, Version = "2.16", SerialNumber = "SN-BBB" }
            }
        });

        Assert.Single(_db.MachineComponents.Where(x => x.Code == "PLC-1"));
        Assert.Equal(2, Cambi().Count);
        Assert.Equal("SN-BBB", _db.MachineComponents.Single(x => x.Code == "PLC-1").SerialNumber);
    }

    // ─── Ore e produzione ───────────────────────────────────────────────────

    /// <summary>
    /// Il primo giorno non ha una differenza da calcolare: scriverci il totalizzatore vorrebbe dire
    /// dichiarare che la macchina ha prodotto in un giorno tutto cio' che ha prodotto in dieci anni.
    /// </summary>
    [Fact]
    public async Task La_prima_lettura_non_produce_un_giorno_di_lavoro()
    {
        var esito = await Manda(Contatori(new DateTime(2026, 8, 10), 12480m, 3_100_000));

        Assert.Null(esito.Counters!.Hours);
        Assert.Null(esito.Counters.Pieces);
        Assert.Equal(12480m, esito.Counters.TotalHours);
    }

    [Fact]
    public async Task Il_giorno_lavorato_e_la_differenza_fra_due_totalizzatori()
    {
        await Manda(Contatori(new DateTime(2026, 8, 10), 12480m, 3_100_000));
        var esito = await Manda(Contatori(new DateTime(2026, 8, 11), 12488m, 3_101_200));

        Assert.Equal(8m, esito.Counters!.Hours);
        Assert.Equal(1200, esito.Counters.Pieces);
    }

    /// <summary>
    /// Il caso per cui si e' scelto il totalizzatore: la macchina non parla per due giorni, e al
    /// ritorno il conto torna lo stesso perche' il totale se lo porta dietro.
    /// </summary>
    [Fact]
    public async Task Due_giorni_saltati_non_perdono_il_lavoro_fatto()
    {
        await Manda(Contatori(new DateTime(2026, 8, 10), 12480m, 3_100_000));
        var esito = await Manda(Contatori(new DateTime(2026, 8, 13), 12504m, 3_103_600));

        Assert.Equal(24m, esito.Counters!.Hours);
        Assert.Equal(3600, esito.Counters.Pieces);
    }

    /// <summary>
    /// Contatore azzerato in assistenza: la differenza sarebbe negativa, cioe' una produzione
    /// negativa in mezzo ai grafici. Si prende il valore attuale e si segna il fatto.
    /// </summary>
    [Fact]
    public async Task Un_contatore_azzerato_si_segna_invece_di_diventare_negativo()
    {
        await Manda(Contatori(new DateTime(2026, 8, 10), 12480m, 3_100_000));
        var esito = await Manda(Contatori(new DateTime(2026, 8, 11), 4m, 500));

        Assert.True(esito.Counters!.CounterReset);
        Assert.Equal(4m, esito.Counters.Hours);
        Assert.Equal(500, esito.Counters.Pieces);
    }

    /// <summary>Rimandare lo stesso giorno corregge quella lettura, non ne crea una seconda.</summary>
    [Fact]
    public async Task Rimandare_lo_stesso_giorno_non_conta_due_volte_la_produzione()
    {
        await Manda(Contatori(new DateTime(2026, 8, 10), 12480m, 3_100_000));
        await Manda(Contatori(new DateTime(2026, 8, 11), 12488m, 3_101_200));
        var esito = await Manda(Contatori(new DateTime(2026, 8, 11), 12490m, 3_101_500));

        Assert.Single(_db.MachineDailyReadings.Where(x => x.Day == new DateTime(2026, 8, 11)));
        Assert.Equal(10m, esito.Counters!.Hours);
        Assert.Equal(1500, esito.Counters.Pieces);
    }

    /// <summary>Le due parti sono indipendenti: si puo' mandare solo i contatori.</summary>
    [Fact]
    public async Task Si_possono_mandare_i_contatori_senza_i_componenti()
    {
        var esito = await Manda(Contatori(new DateTime(2026, 8, 10), 100m, 10));

        Assert.Equal(0, esito.ComponentsReceived);
        Assert.NotNull(esito.Counters);
    }

    public void Dispose() => _db.Dispose();
}
