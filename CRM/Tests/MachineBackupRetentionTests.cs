using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Cosa succede ai backup gia' in archivio quando ne arriva uno nuovo.
/// <para>
/// Qui si cancellano file dei clienti, quindi le regole si provano una per una. Le due che contano:
/// un file identico al precedente non diventa una versione nuova, e la conservazione non tocca mai
/// la PRIMA versione - la configurazione con cui la macchina e' partita, il riferimento per capire
/// cosa e' cambiato da allora.
/// </para>
/// <para>
/// Sui dati veri oggi non ci sono duplicati (25 backup, 25 file diversi) e lo spazio e' 103 MB: il
/// problema non esiste ancora. Nascera' quando le macchine manderanno il backup da sole e una
/// configurazione ferma verra' rispedita ogni giorno.
/// </para>
/// </summary>
public class MachineBackupRetentionTests : IDisposable
{
    private const int IdArticle = 10;

    private readonly ApplicationDbContext _db;
    private readonly IArchiveService _archive = Substitute.For<IArchiveService>();
    private readonly MachineBackupsService _service;

    public MachineBackupRetentionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-retention-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Companies.Add(new Company { Id = 1, RagioneSociale = "Cliente" });
        _db.Products.Add(new Product { Id = 5, Name = "Tornio", Code = "TRN" });
        _db.Articles.Add(new Article { Id = IdArticle, IdCompany = 1, IdProduct = 5, SerialNumber = "2884", Name = "Tornio" });
        _db.SaveChanges();

        _service = new MachineBackupsService(_db, _archive);
    }

    /// <summary>L'impronta la calcola l'archivio salvando: qui si decide cosa deve rispondere.</summary>
    private void ProssimoFileConImpronta(string sha, long size = 1024)
    {
        _archive.SaveStreamAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((size, sha)));
    }

    private Task<CRM.Shared.DTOs.MachineBackupDTO> Carica(string sha)
    {
        ProssimoFileConImpronta(sha);
        return _service.UploadAsync(
            MachineBackupOwnerType.Article,
            IdArticle,
            "backup.bin",
            "application/octet-stream",
            new MemoryStream(new byte[] { 1 }),
            new MachineBackupUploadMetadata(),
            MachineBackupSource.MachineApi,
            "api-key:1");
    }

    private void ConservaVersioni(int quante)
    {
        _db.GlobalSettings.Add(new GlobalSetting { Id = 1, MachineBackupKeepVersions = quante });
        _db.SaveChanges();
    }

    private List<MachineBackup> Versioni()
        => _db.MachineBackups.AsNoTracking().OrderBy(x => x.Version).ToList();

    [Fact]
    public async Task Lo_stesso_file_non_diventa_una_versione_nuova()
    {
        var prima = await Carica("AAA");
        var seconda = await Carica("AAA");

        Assert.Single(Versioni());
        Assert.Equal(prima.Id, seconda.Id);
    }

    /// <summary>Contro-prova: senza, il test qui sopra passerebbe anche se non salvasse mai nulla.</summary>
    [Fact]
    public async Task Un_file_diverso_diventa_una_versione_nuova()
    {
        await Carica("AAA");
        await Carica("BBB");

        Assert.Equal(new[] { 1, 2 }, Versioni().Select(x => x.Version).ToArray());
    }

    /// <summary>
    /// Due backup uguali a distanza, con altri diversi in mezzo, dicono che la configurazione e'
    /// tornata indietro: e' un fatto, e va tenuto.
    /// </summary>
    [Fact]
    public async Task Un_ritorno_a_una_configurazione_precedente_si_conserva()
    {
        await Carica("AAA");
        await Carica("BBB");
        await Carica("AAA");

        Assert.Equal(3, Versioni().Count);
    }

    [Fact]
    public async Task Senza_impostazione_non_si_cancella_niente()
    {
        for (var i = 0; i < 5; i++)
            await Carica($"SHA{i}");

        Assert.Equal(5, Versioni().Count);
        _archive.DidNotReceive().Delete(Arg.Any<int>(), Arg.Any<string>());
    }

    /// <summary>
    /// Il punto della scelta: si tengono le ultime N, ma la prima resta. Con 2 versioni da tenere e
    /// 5 caricate restano la 1 (la configurazione di partenza), la 4 e la 5.
    /// </summary>
    [Fact]
    public async Task La_prima_versione_non_si_cancella_mai()
    {
        ConservaVersioni(2);

        for (var i = 0; i < 5; i++)
            await Carica($"SHA{i}");

        Assert.Equal(new[] { 1, 4, 5 }, Versioni().Select(x => x.Version).ToArray());
    }

    /// <summary>La riga sparisce e il file pure: lasciarlo li' vorrebbe dire pagare spazio per niente.</summary>
    [Fact]
    public async Task Le_versioni_eccedenti_spariscono_anche_dall_archivio()
    {
        ConservaVersioni(2);

        for (var i = 0; i < 4; i++)
            await Carica($"SHA{i}");

        // Caricate 4, tenute 1-3-4: la sola eccedente e' la versione 2.
        _archive.Received(1).Delete(Arg.Any<int>(), "backup.bin");
    }

    public void Dispose() => _db.Dispose();
}
