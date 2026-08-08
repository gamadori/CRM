using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// La cartella delle note spese di un'iniziativa (fiera, trasferta).
/// <para>
/// A differenza di intervento e attivita', l'iniziativa e' un contenitore <b>condiviso</b>: le
/// spese sono di piu' persone. Qui si verifica che il vincolo di visibilita' sia lo stesso
/// dell'elenco generale - ognuno le proprie, chi ne ha diritto tutte - perche' e' l'unico punto
/// da cui passano queste letture.
/// </para>
/// </summary>
public class ExpenseInitiativeFolderTests : IDisposable
{
    private const int Fiera = 7;
    private const int AltraFiera = 8;
    private const string Io = "io";
    private const string Collega = "collega";

    private readonly ApplicationDbContext _db;
    private readonly ExpenseReceiptService _service;

    public ExpenseInitiativeFolderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"expense-initiative-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _db.GlobalSettings.Add(new GlobalSetting { Id = 1, BaseCurrency = "EUR" });
        _db.Users.AddRange(
            new ApplicationUser { Id = Io, UserName = Io, Email = "io@test.local" },
            new ApplicationUser { Id = Collega, UserName = Collega, Email = "collega@test.local" });
        _db.SaveChanges();

        var rates = Substitute.For<IExchangeRateService>();
        rates.GetRateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()).Returns((decimal?)1m);

        _service = new ExpenseReceiptService(_db, rates);
    }

    public void Dispose() => _db.Dispose();

    private async Task Spesa(string chi, decimal importo, int? iniziativa = Fiera)
    {
        await _service.CreateAsync(new ExpenseReceiptCreateUpdateDTO
        {
            IdUserSpender = chi,
            IdInitiative = iniziativa,
            TotalAmount = importo,
            Currency = "EUR",
            TransactionDate = DateTime.Today,
            Category = ExpenseCategory.Lodging
        }, chi);
    }

    [Fact]
    public async Task La_cartella_contiene_solo_le_spese_di_quella_iniziativa()
    {
        await Spesa(Io, 100m);
        await Spesa(Io, 50m, AltraFiera);
        await Spesa(Io, 30m, iniziativa: null);

        var cartella = await _service.GetByInitiativeIdAsync(Fiera, restrictToUserId: null);

        Assert.Single(cartella);
        Assert.Equal(100m, cartella[0].TotalAmount);
    }

    [Fact]
    public async Task Chi_vede_tutto_trova_anche_le_spese_dei_colleghi()
    {
        await Spesa(Io, 100m);
        await Spesa(Collega, 250m);

        var cartella = await _service.GetByInitiativeIdAsync(Fiera, restrictToUserId: null);
        var riepilogo = await _service.GetSummaryByInitiativeIdAsync(Fiera, restrictToUserId: null);

        Assert.Equal(2, cartella.Count);
        Assert.Equal(350m, riepilogo.TotalExpenses);

        // Lo spaccato per persona e' il motivo per cui la cartella esiste: dice a chi va
        // rimborsato cosa.
        Assert.Equal(2, riepilogo.ByUser.Count);
        Assert.Equal(250m, riepilogo.ByUser.First().TotalBase);
    }

    [Fact]
    public async Task Chi_vede_solo_le_proprie_non_trova_quelle_dei_colleghi()
    {
        await Spesa(Io, 100m);
        await Spesa(Collega, 250m);

        var cartella = await _service.GetByInitiativeIdAsync(Fiera, restrictToUserId: Io);
        var riepilogo = await _service.GetSummaryByInitiativeIdAsync(Fiera, restrictToUserId: Io);

        Assert.Single(cartella);
        Assert.Equal(100m, cartella[0].TotalAmount);

        // Il totale della cartella segue le righe visibili: e' il motivo per cui la pagina deve
        // dichiarare "vedi solo le tue", altrimenti la differenza con il consuntivo
        // dell'iniziativa - che le conta tutte - si legge come un errore dei conti.
        Assert.Equal(100m, riepilogo.TotalExpenses);
    }

    [Fact]
    public async Task Una_iniziativa_senza_spese_da_una_cartella_vuota_non_un_errore()
    {
        var cartella = await _service.GetByInitiativeIdAsync(Fiera, restrictToUserId: null);
        var riepilogo = await _service.GetSummaryByInitiativeIdAsync(Fiera, restrictToUserId: null);

        Assert.Empty(cartella);
        Assert.Equal(0, riepilogo.TotalReceiptsCount);
        Assert.Equal("EUR", riepilogo.BaseCurrency);
    }
}
