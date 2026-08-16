using CRM.Server.Controllers;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Cosa vede una macchina che si presenta con la sua chiave.
/// <para>
/// Il difetto presidiato: l'API non aveva perimetro. L'elenco rispondeva con le macchine di tutti i
/// clienti, e per scaricare o caricare il backup di una macchina bastava conoscerne il numero di
/// riga. Nessuno se n'era accorto perche' chiavi di questo ambito non ne erano mai state emesse: la
/// prima installata da un cliente avrebbe aperto a quel cliente i backup di tutti gli altri.
/// </para>
/// <para>
/// Sono regole di permesso, dove sbagliare non da' errore: da' a qualcuno una porta che non doveva
/// avere. Per questo si provano una per una.
/// </para>
/// </summary>
public class MachineBackupApiTests : IDisposable
{
    private const int DittaMia = 1;
    private const int DittaAltrui = 2;
    private const string ChiaveInChiaro = "crmtk_prova";

    private readonly ApplicationDbContext _db;
    private readonly IApiKeyService _apiKeys = Substitute.For<IApiKeyService>();
    private readonly IMachineBackupsService _backups = Substitute.For<IMachineBackupsService>();

    public MachineBackupApiTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-machine-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);

        _db.Companies.AddRange(
            new Company { Id = DittaMia, RagioneSociale = "Cliente uno" },
            new Company { Id = DittaAltrui, RagioneSociale = "Cliente due" });

        _db.Products.Add(new Product { Id = 5, Name = "Tornio", Code = "TRN" });

        _db.Articles.AddRange(
            new Article { Id = 10, IdCompany = DittaMia, IdProduct = 5, SerialNumber = "2884", Name = "Tornio uno" },
            new Article { Id = 20, IdCompany = DittaAltrui, IdProduct = 5, SerialNumber = "2902", Name = "Tornio due" });

        _db.SaveChanges();
    }

    /// <summary>La chiave vale per la sua ditta. Senza ditta non apre niente.</summary>
    private void ChiaveDi(int? idCompany, ApiKeyPermission permesso = ApiKeyPermission.ReadWrite)
    {
        var key = new ApiKey
        {
            Id = 77,
            Name = "Linea collaudo",
            Scope = ApiKeyScope.Machine,
            Permission = permesso,
            IdCompany = idCompany
        };

        _apiKeys.ValidateAsync(ChiaveInChiaro, ApiKeyScope.Machine, Arg.Any<ApiKeyPermission?>())
            .Returns(call =>
            {
                var richiesto = call.ArgAt<ApiKeyPermission?>(2);
                if (richiesto == ApiKeyPermission.ReadWrite && key.Permission != ApiKeyPermission.ReadWrite)
                    return (ApiKey?)null;
                return key;
            });
    }

    private MachineParametersController Controller()
    {
        var controller = new MachineParametersController(_db, _apiKeys, _backups, new MachineStatusService(_db));
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Api-Key"] = ChiaveInChiaro;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task L_elenco_mostra_solo_le_macchine_della_ditta_della_chiave()
    {
        ChiaveDi(DittaMia);

        var risposta = await Controller().GetArticles(new MachineArticleListFilter());

        var elenco = Assert.IsType<List<MachineArticleDTO>>(((OkObjectResult)risposta.Result!).Value);
        Assert.Equal(new[] { "2884" }, elenco.Select(x => x.SerialNumber).ToArray());
    }

    /// <summary>
    /// Una chiave senza ditta vedrebbe il parco macchine di chiunque: non si prosegue, invece di
    /// ripiegare su "allora le mostro tutte".
    /// </summary>
    [Fact]
    public async Task Una_chiave_senza_ditta_non_apre_niente()
    {
        ChiaveDi(null);

        var risposta = await Controller().GetArticles(new MachineArticleListFilter());

        Assert.IsType<UnauthorizedResult>(risposta.Result);
    }

    [Fact]
    public async Task La_macchina_si_trova_per_matricola()
    {
        ChiaveDi(DittaMia);
        _backups.GetLatestAsync(MachineBackupOwnerType.Article, 10)
            .Returns(new MachineBackupDTO { Id = 3, Version = 7 });

        var risposta = await Controller().GetLatestArticleBackup("2884");

        var backup = Assert.IsType<MachineBackupDTO>(((OkObjectResult)risposta).Value);
        Assert.Equal(7, backup.Version);
    }

    /// <summary>
    /// La matricola di un'altra ditta risponde come una inesistente: dire "esiste ma non e' tua"
    /// sarebbe gia' un'informazione sul parco macchine altrui.
    /// </summary>
    [Fact]
    public async Task La_matricola_di_un_altra_ditta_non_esiste()
    {
        ChiaveDi(DittaMia);

        var risposta = await Controller().GetLatestArticleBackup("2902");

        Assert.IsType<NotFoundResult>(risposta);
        await _backups.DidNotReceive().GetLatestAsync(Arg.Any<MachineBackupOwnerType>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Non_si_carica_un_backup_sulla_macchina_di_un_altra_ditta()
    {
        ChiaveDi(DittaMia);

        var risposta = await Controller().UploadArticleBackup("2902", FileFinto(), null, null, CancellationToken.None);

        Assert.IsType<NotFoundResult>(risposta.Result);
        await _backups.DidNotReceiveWithAnyArgs().UploadAsync(
            default, default, default!, default!, default!, default!, default, default, default);
    }

    /// <summary>Il backup di un'altra ditta non si scarica nemmeno conoscendone il numero.</summary>
    [Fact]
    public async Task Un_backup_altrui_non_si_scarica_per_numero()
    {
        ChiaveDi(DittaMia);
        _db.MachineBackups.Add(new MachineBackup
        {
            Id = 99,
            OwnerType = MachineBackupOwnerType.Article,
            IdArticle = 20,
            FileName = "altrui.bin",
            Sha256 = "x",
            Version = 1
        });
        _db.SaveChanges();

        var risposta = await Controller().Download(99);

        Assert.IsType<NotFoundResult>(risposta);
        await _backups.DidNotReceive().DownloadAsync(Arg.Any<int>());
    }

    /// <summary>Contro-prova: sul proprio backup lo scarico avviene davvero.</summary>
    [Fact]
    public async Task Il_proprio_backup_si_scarica()
    {
        ChiaveDi(DittaMia);
        _db.MachineBackups.Add(new MachineBackup
        {
            Id = 98,
            OwnerType = MachineBackupOwnerType.Article,
            IdArticle = 10,
            FileName = "mio.bin",
            Sha256 = "y",
            Version = 1
        });
        _db.SaveChanges();

        _backups.DownloadAsync(98).Returns((new MemoryStream(new byte[] { 1, 2, 3 }) as Stream, "application/octet-stream", "mio.bin"));

        var risposta = await Controller().Download(98);

        Assert.IsType<FileStreamResult>(risposta);
    }

    /// <summary>
    /// Il backup di riferimento del modello si raggiunge dalla matricola: e' l'unica cosa che la
    /// macchina sa di se stessa.
    /// </summary>
    [Fact]
    public async Task Il_backup_del_modello_si_raggiunge_dalla_matricola()
    {
        ChiaveDi(DittaMia);
        _backups.GetLatestAsync(MachineBackupOwnerType.Product, 5)
            .Returns(new MachineBackupDTO { Id = 4, Version = 2 });

        var risposta = await Controller().GetLatestModelBackup("2884");

        var backup = Assert.IsType<MachineBackupDTO>(((OkObjectResult)risposta).Value);
        Assert.Equal(2, backup.Version);
    }

    /// <summary>Una chiave di sola lettura non carica.</summary>
    [Fact]
    public async Task Una_chiave_in_sola_lettura_non_carica()
    {
        ChiaveDi(DittaMia, ApiKeyPermission.ReadOnly);

        var risposta = await Controller().UploadArticleBackup("2884", FileFinto(), null, null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(risposta.Result);
    }

    private static IFormFile FileFinto()
    {
        var contenuto = new MemoryStream(new byte[] { 9, 9, 9 });
        return new FormFile(contenuto, 0, contenuto.Length, "file", "backup.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/octet-stream"
        };
    }

    public void Dispose() => _db.Dispose();
}
