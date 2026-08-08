using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Il prospetto delle note spese raggruppate per tipologia.
/// <para>
/// Si verificano i conti, non l'impaginazione: il PDF e' solo la resa di questi dati, e i numeri
/// sulla carta devono essere gli stessi che si vedono a schermo.
/// </para>
/// </summary>
public class ExpenseReportTests : IDisposable
{
    private const int Fiera = 3;
    private const string Io = "io";
    private const string Collega = "collega";

    private readonly ApplicationDbContext _db;
    private readonly ExpenseReceiptService _service;
    private readonly IExchangeRateService _rates = Substitute.For<IExchangeRateService>();

    public ExpenseReportTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"expense-report-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _db.GlobalSettings.Add(new GlobalSetting { Id = 1, BaseCurrency = "EUR" });
        _db.Users.AddRange(
            new ApplicationUser { Id = Io, UserName = Io, Email = "io@test.local", Name = "Gianluca", Surname = "Amadori" },
            new ApplicationUser { Id = Collega, UserName = Collega, Email = "collega@test.local", Name = "Anna", Surname = "Bianchi" });
        _db.SaveChanges();

        // Cambio noto: cosi' i totali del prospetto si possono affermare, non solo osservare.
        _rates.GetRateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()).Returns((decimal?)1m);

        _service = new ExpenseReceiptService(_db, _rates);
    }

    public void Dispose() => _db.Dispose();

    private async Task<ExpenseReceiptDTO> Spesa(
        ExpenseCategory? tipologia,
        decimal importo,
        string chi = Io,
        string valuta = "EUR",
        int? iniziativa = Fiera,
        DateTime? data = null)
    {
        return await _service.CreateAsync(new ExpenseReceiptCreateUpdateDTO
        {
            IdUserSpender = chi,
            IdInitiative = iniziativa,
            TotalAmount = importo,
            Currency = valuta,
            TransactionDate = data ?? DateTime.Today,
            MerchantName = $"Esercente {importo:0}",
            Category = tipologia
        }, chi);
    }

    private Task<ExpenseReportData> Prospetto(ExpenseReceiptFilter filter = null, string restrictToUserId = null)
        => _service.BuildReportDataAsync(filter ?? new ExpenseReceiptFilter(), restrictToUserId);

    // ─── Raggruppamento ──────────────────────────────────────────────────────

    [Fact]
    public async Task Le_spese_si_raggruppano_per_tipologia_con_il_loro_subtotale()
    {
        await Spesa(ExpenseCategory.Lodging, 200m);
        await Spesa(ExpenseCategory.Lodging, 100m);
        await Spesa(ExpenseCategory.Meals, 40m);

        var report = await Prospetto();

        Assert.Equal(2, report.Groups.Count);

        var alloggio = report.Groups.Single(g => g.Category == ExpenseCategory.Lodging);
        Assert.Equal(300m, alloggio.TotalBase);
        Assert.Equal(2, alloggio.Rows.Count);
        Assert.Equal("Alloggio", alloggio.Label);

        Assert.Equal(340m, report.TotalBase);
        Assert.Equal(3, report.RowCount);
    }

    [Fact]
    public async Task I_gruppi_piu_pesanti_vengono_prima()
    {
        await Spesa(ExpenseCategory.Meals, 40m);
        await Spesa(ExpenseCategory.Lodging, 300m);

        var report = await Prospetto();

        Assert.Equal(ExpenseCategory.Lodging, report.Groups.First().Category);
    }

    [Fact]
    public async Task Le_spese_senza_tipologia_sono_un_gruppo_e_stanno_in_fondo()
    {
        // Non si nascondono: un prospetto che le tace non somma al totale, e sono proprio quelle
        // su cui c'e' da lavorare prima di chiudere un rimborso.
        await Spesa(tipologia: null, 500m);
        await Spesa(ExpenseCategory.Meals, 40m);

        var report = await Prospetto();

        var ultimo = report.Groups.Last();
        Assert.Null(ultimo.Category);
        Assert.Equal("Da indicare", ultimo.Label);
        Assert.Equal(500m, ultimo.TotalBase);
        Assert.Equal(540m, report.TotalBase);
    }

    // ─── Spese non convertite ────────────────────────────────────────────────

    [Fact]
    public async Task Una_spesa_senza_cambio_e_elencata_ma_fuori_dai_totali()
    {
        _rates.GetRateAsync("USD", Arg.Any<string>(), Arg.Any<DateTime>()).Returns((decimal?)null);

        await Spesa(ExpenseCategory.Lodging, 100m);
        await Spesa(ExpenseCategory.Lodging, 250m, valuta: "USD");

        var report = await Prospetto();

        var alloggio = report.Groups.Single();

        // La riga c'e'...
        Assert.Equal(2, alloggio.Rows.Count);
        Assert.Contains(alloggio.Rows, r => r.NeedsConversion);

        // ...ma nel subtotale no, ed e' dichiarata.
        Assert.Equal(100m, alloggio.TotalBase);
        Assert.Equal(1, alloggio.NeedsConversionCount);
        Assert.Equal(100m, report.TotalBase);
        Assert.Equal(1, report.NeedsConversionCount);
    }

    // ─── Insieme stampato ────────────────────────────────────────────────────

    [Fact]
    public async Task Il_prospetto_stampa_solo_le_spese_del_contenitore_chiesto()
    {
        await Spesa(ExpenseCategory.Meals, 40m);
        await Spesa(ExpenseCategory.Meals, 90m, iniziativa: 99);

        var report = await Prospetto(new ExpenseReceiptFilter { IdInitiative = Fiera });

        Assert.Equal(1, report.RowCount);
        Assert.Equal(40m, report.TotalBase);
    }

    [Fact]
    public async Task Il_prospetto_rispetta_il_periodo_e_lo_scrive_in_testa()
    {
        await Spesa(ExpenseCategory.Meals, 40m, data: new DateTime(2026, 3, 10));
        await Spesa(ExpenseCategory.Meals, 90m, data: new DateTime(2026, 6, 10));

        var report = await Prospetto(new ExpenseReceiptFilter
        {
            DateFrom = new DateTime(2026, 3, 1),
            DateTo = new DateTime(2026, 3, 31)
        });

        Assert.Equal(1, report.RowCount);
        Assert.Contains(report.Context, x => x.Contains("dal 01/03/2026") && x.Contains("al 31/03/2026"));
    }

    [Fact]
    public async Task Chi_vede_solo_le_proprie_stampa_solo_le_proprie_e_il_foglio_lo_dichiara()
    {
        await Spesa(ExpenseCategory.Meals, 40m, chi: Io);
        await Spesa(ExpenseCategory.Meals, 90m, chi: Collega);

        var report = await Prospetto(restrictToUserId: Io);

        Assert.Equal(1, report.RowCount);
        Assert.Equal(40m, report.TotalBase);
        Assert.True(report.PartialView);
    }

    [Fact]
    public async Task La_colonna_persona_compare_solo_se_ce_n_e_piu_d_una()
    {
        await Spesa(ExpenseCategory.Meals, 40m, chi: Io);
        Assert.False((await Prospetto()).ShowSpender);

        await Spesa(ExpenseCategory.Meals, 90m, chi: Collega);
        Assert.True((await Prospetto()).ShowSpender);
    }

    [Fact]
    public async Task Senza_spese_il_prospetto_non_ha_righe_e_il_chiamante_puo_non_stamparlo()
    {
        var report = await Prospetto();

        Assert.Equal(0, report.RowCount);
        Assert.Empty(report.Groups);
        Assert.Equal(0m, report.TotalBase);
    }

    [Fact]
    public async Task Il_nome_del_file_porta_il_titolo_ripulito()
    {
        await Spesa(ExpenseCategory.Meals, 40m);

        var report = await Prospetto();

        Assert.StartsWith("NoteSpese-", report.FileName);
        Assert.EndsWith(".pdf", report.FileName);
        Assert.DoesNotContain(" ", report.FileName);
    }
}

/// <summary>
/// La stampa vera e propria: qui interessa solo che il PDF esca, e con quello che c'e' scritto
/// nei dati. Un prospetto che non si genera e' peggio di un prospetto brutto.
/// </summary>
public class ExpenseReportPdfTests
{
    [Fact]
    public void Il_prospetto_produce_un_pdf_valido()
    {
        var data = new ExpenseReportData
        {
            Title = "Fiera Saca",
            BaseCurrency = "EUR",
            RowCount = 2,
            TotalBase = 340m,
            ShowSpender = true,
            Context = new List<string> { "Periodo: tutte le date" },
            Groups = new List<ExpenseReportGroup>
            {
                new()
                {
                    Category = ExpenseCategory.Lodging,
                    Label = "Alloggio",
                    TotalBase = 300m,
                    Rows = new List<ExpenseReportRow>
                    {
                        new()
                        {
                            Date = DateTime.Today,
                            SpenderName = "Amadori Gianluca",
                            MerchantName = "Contoso Inn",
                            Context = "Fiera Saca",
                            Amount = 300m,
                            Currency = "EUR",
                            AmountBase = 300m
                        }
                    }
                },
                new()
                {
                    Category = null,
                    Label = "Da indicare",
                    TotalBase = 40m,
                    NeedsConversionCount = 1,
                    Rows = new List<ExpenseReportRow>
                    {
                        new()
                        {
                            Date = DateTime.Today,
                            SpenderName = "Bianchi Anna",
                            MerchantName = "Esercente ignoto",
                            Context = "Costo generale",
                            Amount = 40m,
                            Currency = "USD"
                        }
                    }
                }
            }
        };

        var bytes = new ExpenseReportPdfGenerator().Generate(data);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, "un PDF con due gruppi non puo' essere lungo poche centinaia di byte");

        // Firma del formato: se cambiasse il generatore, questo resta il minimo verificabile.
        Assert.Equal(new byte[] { 0x25, 0x50, 0x44, 0x46 }, bytes.Take(4).ToArray());
    }
}
