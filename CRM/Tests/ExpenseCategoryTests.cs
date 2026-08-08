using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Server.Services.ExpenseCategorization;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Tipologia della nota spese. Il modello e' sostituito: qui si verifica quello che deve restare
/// vero comunque risponda l'AI - la cascata dei tre livelli, la soglia, e il fatto che una spesa
/// si registri comunque anche quando la classificazione non riesce.
/// </summary>
public class ExpenseCategoryRulesTests
{
    private static ExpenseCategoryRequest Documento(
        string? esercente = null,
        string? tipo = null,
        params string[] righe) =>
        new(esercente, tipo, righe);

    // ─── Livello 1: sottotipo del documento ──────────────────────────────────

    [Theory]
    [InlineData("receipt.hotel", ExpenseCategory.Lodging)]
    [InlineData("receipt.gas", ExpenseCategory.Fuel)]
    [InlineData("receipt.parking", ExpenseCategory.Parking)]
    [InlineData("receipt.retailMeal", ExpenseCategory.Meals)]
    public void Il_sottotipo_riconosciuto_dall_OCR_diventa_una_tipologia(string tipo, ExpenseCategory attesa)
    {
        var esito = ExpenseCategoryRules.Apply(Documento(tipo: tipo));

        Assert.Equal(attesa, esito.Category);
        Assert.Equal(ExpenseCategorySource.DocumentType, esito.Source);
    }

    [Theory]
    [InlineData("invoice")]
    [InlineData("receipt")]
    [InlineData("receipt.creditCard")]
    public void I_tipi_che_non_dicono_niente_sulla_spesa_non_propongono_niente(string tipo)
    {
        // receipt.creditCard e' la ricevuta del pagamento: dice COME si e' pagato, non CHE COSA.
        Assert.Null(ExpenseCategoryRules.Apply(Documento(tipo: tipo)).Category);
    }

    [Fact]
    public void Un_file_con_documenti_di_tipo_diverso_non_ha_una_tipologia_sola()
    {
        Assert.Null(ExpenseCategoryRules.FromDocumentType("receipt.hotel, receipt.gas").Category);
    }

    // ─── Livello 2: esercente e righe ────────────────────────────────────────

    [Theory]
    [InlineData("Autogrill Villoresi Est", ExpenseCategory.Meals)]
    [InlineData("Q8 Stazione di servizio", ExpenseCategory.Fuel)]
    [InlineData("Telepass S.p.A.", ExpenseCategory.Tolls)]
    [InlineData("Hotel Corona d'Oro", ExpenseCategory.Lodging)]
    [InlineData("Trenitalia", ExpenseCategory.Travel)]
    [InlineData("Parcheggio Centrale", ExpenseCategory.Parking)]
    [InlineData("Cartoleria Bianchi", ExpenseCategory.Supplies)]
    public void L_esercente_riconosciuto_da_la_tipologia(string esercente, ExpenseCategory attesa)
    {
        var esito = ExpenseCategoryRules.Apply(Documento(esercente));

        Assert.Equal(attesa, esito.Category);
        Assert.Equal(ExpenseCategorySource.MerchantRule, esito.Source);
        Assert.Contains(esercente, esito.Reason);
    }

    [Fact]
    public void L_esercente_vince_sul_sottotipo_del_documento()
    {
        // Caso vero: l'area di servizio vende anche panini, e l'OCR la legge come "pasto al
        // dettaglio". Il nome dell'esercente invece non e' ambiguo.
        var esito = ExpenseCategoryRules.Apply(Documento("Q8 Area di servizio", tipo: "receipt.retailMeal"));

        Assert.Equal(ExpenseCategory.Fuel, esito.Category);
    }

    [Fact]
    public void Senza_esercente_ne_sottotipo_decidono_le_righe()
    {
        var esito = ExpenseCategoryRules.Apply(Documento(righe: new[] { "Gasolio self service", "Litri 42,3" }));

        Assert.Equal(ExpenseCategory.Fuel, esito.Category);
        Assert.Equal(ExpenseCategorySource.MerchantRule, esito.Source);
    }

    [Fact]
    public void Le_righe_valgono_meno_dell_esercente()
    {
        var daEsercente = ExpenseCategoryRules.Apply(Documento("Ristorante Da Mario"));
        var daRighe = ExpenseCategoryRules.Apply(Documento(righe: new[] { "Coperto" }));

        Assert.True(daEsercente.Confidence > daRighe.Confidence);
    }

    [Fact]
    public void Vince_la_tipologia_che_compare_in_piu_righe()
    {
        // Il conto dell'albergo ha una riga di caffe' e tre di pernottamento: la spesa e' l'albergo.
        var esito = ExpenseCategoryRules.Apply(Documento(righe: new[]
        {
            "Caffè al bancone",
            "Pernottamento 12/03",
            "Pernottamento 13/03",
            "Pernottamento 14/03"
        }));

        Assert.Equal(ExpenseCategory.Lodging, esito.Category);
    }

    [Fact]
    public void Due_tipologie_a_pari_merito_non_propongono_niente()
    {
        // Spesa mista: sceglierne una a caso sarebbe peggio che lasciar decidere.
        var esito = ExpenseCategoryRules.Apply(Documento(righe: new[] { "Pernottamento", "Gasolio" }));

        Assert.Null(esito.Category);
    }

    [Fact]
    public void Le_sigle_ambigue_valgono_solo_come_nome_dell_esercente()
    {
        // "ATM" nel nome dell'esercente e' l'azienda dei trasporti; dentro una riga e' il bancomat.
        Assert.Equal(ExpenseCategory.Travel, ExpenseCategoryRules.Apply(Documento("ATM Milano")).Category);
        Assert.Null(ExpenseCategoryRules.Apply(Documento(righe: new[] { "Prelievo ATM" })).Category);
    }

    [Fact]
    public void Nessuna_regola_propone_mai_la_rappresentanza()
    {
        // Sta nell'occasione, non sullo scontrino: proporla sbaglierebbe la deducibilita'.
        var proposte = new[]
        {
            ExpenseCategoryRules.Apply(Documento("Ristorante Il Convivio")),
            ExpenseCategoryRules.Apply(Documento("Hotel Excelsior")),
            ExpenseCategoryRules.Apply(Documento(righe: new[] { "Cena" }))
        };

        Assert.DoesNotContain(ExpenseCategory.Entertainment, proposte.Select(p => p.Category));
    }

    [Fact]
    public void Un_documento_che_non_dice_niente_resta_senza_tipologia()
    {
        Assert.Null(ExpenseCategoryRules.Apply(Documento("XYZ S.r.l.", "invoice", "Fornitura")).Category);
    }
}

/// <summary>
/// La cascata dei tre livelli e la soglia: e' la parte che decide quando si spende una chiamata
/// al modello e quando una proposta e' troppo incerta per essere mostrata.
/// </summary>
public class ExpenseCategorizerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly IExpenseCategoryAiClient _ai = Substitute.For<IExpenseCategoryAiClient>();
    private readonly ExpenseCategorizer _service;

    public ExpenseCategorizerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"expense-category-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _ai.IsAvailable.Returns(true);
        _ai.Model.Returns("modello-test");

        _service = new ExpenseCategorizer(_db, _ai, Substitute.For<ILogger<ExpenseCategorizer>>());
    }

    public void Dispose() => _db.Dispose();

    private void Impostazioni(bool ai = true, double soglia = 0.6)
    {
        _db.GlobalSettings.Add(new GlobalSetting
        {
            Id = 1,
            ExpenseCategoryAiEnabled = ai,
            ExpenseCategoryMinConfidence = soglia
        });
        _db.SaveChanges();
    }

    private void Risponde(params ExpenseCategory?[] categorie)
        => _ai.SuggestAsync(Arg.Any<IReadOnlyList<ExpenseCategoryRequest>>(), Arg.Any<CancellationToken>())
            .Returns(categorie
                .Select(c => new ExpenseCategorySuggestion(c, 0.8, "motivo", ExpenseCategorySource.Ai))
                .ToList());

    private static ExpenseCategoryRequest Riconoscibile => new("Hotel Roma", null, Array.Empty<string>());

    private static ExpenseCategoryRequest Sconosciuto => new("XYZ S.r.l.", null, Array.Empty<string>());

    [Fact]
    public async Task Se_le_regole_rispondono_il_modello_non_viene_nemmeno_chiamato()
    {
        Impostazioni();

        var esito = await _service.CategorizeAsync(new[] { Riconoscibile });

        Assert.Equal(ExpenseCategory.Lodging, esito[0].Category);
        Assert.Equal(ExpenseCategorySource.MerchantRule, esito[0].Source);
        await _ai.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default);
    }

    [Fact]
    public async Task Al_modello_arrivano_solo_i_documenti_rimasti_scoperti()
    {
        Impostazioni();
        Risponde(ExpenseCategory.Supplies);

        var esito = await _service.CategorizeAsync(new[] { Riconoscibile, Sconosciuto });

        var inviati = (IReadOnlyList<ExpenseCategoryRequest>)_ai.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IExpenseCategoryAiClient.SuggestAsync))
            .GetArguments()[0]!;

        Assert.Single(inviati);
        Assert.Equal("XYZ S.r.l.", inviati[0].MerchantName);

        // Ogni proposta resta al posto del suo documento.
        Assert.Equal(ExpenseCategory.Lodging, esito[0].Category);
        Assert.Equal(ExpenseCategory.Supplies, esito[1].Category);
        Assert.Equal(ExpenseCategorySource.Ai, esito[1].Source);
    }

    [Fact]
    public async Task Con_l_AI_spenta_restano_solo_le_regole()
    {
        Impostazioni(ai: false);

        var esito = await _service.CategorizeAsync(new[] { Riconoscibile, Sconosciuto });

        Assert.Equal(ExpenseCategory.Lodging, esito[0].Category);
        Assert.Null(esito[1].Category);
        await _ai.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default);
    }

    [Fact]
    public async Task Senza_impostazioni_l_AI_non_si_accende_da_sola()
    {
        var esito = await _service.CategorizeAsync(new[] { Sconosciuto });

        Assert.Null(esito[0].Category);
        await _ai.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default);
    }

    [Fact]
    public async Task Sotto_la_soglia_la_tipologia_viene_scartata()
    {
        // Meglio un campo da compilare che una voce di rimborso messa li' senza convinzione:
        // quella nessuno la ricontrolla, ed e' il campo da cui dipende la deducibilita'.
        Impostazioni(soglia: 0.95);

        var esito = await _service.CategorizeAsync(new[] { Riconoscibile });

        Assert.Null(esito[0].Category);
    }

    [Fact]
    public async Task Un_errore_del_modello_non_impedisce_di_registrare_la_spesa()
    {
        Impostazioni();
        _ai.SuggestAsync(Arg.Any<IReadOnlyList<ExpenseCategoryRequest>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ExpenseCategorySuggestion>?>(_ => throw new InvalidOperationException("modello non raggiungibile"));

        var esito = await _service.CategorizeAsync(new[] { Riconoscibile, Sconosciuto });

        Assert.Equal(ExpenseCategory.Lodging, esito[0].Category);
        Assert.Null(esito[1].Category);
    }
}

/// <summary>
/// Che cosa resta scritto quando una persona conferma o corregge la proposta: e' l'unico modo di
/// sapere, fra un mese, se l'automatismo ci prende davvero.
/// </summary>
public class ExpenseCategoryPersistenceTests : IDisposable
{
    private const string Utente = "utente-corrente";

    private readonly ApplicationDbContext _db;
    private readonly ExpenseReceiptService _service;

    public ExpenseCategoryPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"expense-receipt-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new ApplicationDbContext(options);
        _db.GlobalSettings.Add(new GlobalSetting { Id = 1, BaseCurrency = "EUR" });
        _db.SaveChanges();

        var rates = Substitute.For<IExchangeRateService>();
        rates.GetRateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()).Returns((decimal?)1m);

        _service = new ExpenseReceiptService(_db, rates);
    }

    public void Dispose() => _db.Dispose();

    private static ExpenseReceiptCreateUpdateDTO Nuova(
        ExpenseCategory? scelta,
        ExpenseCategory? proposta,
        ExpenseCategorySource? provenienza) => new()
        {
            IdUserSpender = Utente,
            TotalAmount = 42m,
            Currency = "EUR",
            TransactionDate = DateTime.Today,
            Category = scelta,
            CategorySuggested = proposta,
            CategorySource = provenienza,
            CategoryConfidence = 0.9,
            CategoryReason = "L'esercente «Hotel Roma» è riconosciuto come Alloggio."
        };

    [Fact]
    public async Task Accettare_la_proposta_ne_conserva_la_provenienza()
    {
        var creata = await _service.CreateAsync(
            Nuova(ExpenseCategory.Lodging, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule),
            Utente);

        Assert.Equal(ExpenseCategory.Lodging, creata.Category);
        Assert.Equal(ExpenseCategorySource.MerchantRule, creata.CategorySource);
        Assert.Equal(0.9, creata.CategoryConfidence);
    }

    [Fact]
    public async Task Correggere_la_proposta_la_registra_come_scelta_manuale()
    {
        var creata = await _service.CreateAsync(
            Nuova(ExpenseCategory.Meals, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule),
            Utente);

        Assert.Equal(ExpenseCategory.Meals, creata.Category);
        Assert.Equal(ExpenseCategorySource.Manual, creata.CategorySource);

        // La proposta resta: e' il termine di paragone con cui si contano le correzioni.
        Assert.Equal(ExpenseCategory.Lodging, creata.CategorySuggested);

        // Confidenza e motivo parlavano di un valore che non c'e' piu'.
        Assert.Null(creata.CategoryConfidence);
        Assert.Null(creata.CategoryReason);
    }

    [Fact]
    public async Task Il_client_non_puo_spacciare_per_automatica_una_scelta_sua()
    {
        // Nessuna proposta era mai stata fatta: qualunque cosa dichiari il client, la tipologia
        // l'ha scelta una persona.
        var creata = await _service.CreateAsync(
            Nuova(ExpenseCategory.Fuel, proposta: null, provenienza: ExpenseCategorySource.Ai),
            Utente);

        Assert.Equal(ExpenseCategorySource.Manual, creata.CategorySource);
    }

    [Fact]
    public async Task Togliere_la_tipologia_svuota_anche_la_sua_provenienza()
    {
        var creata = await _service.CreateAsync(
            Nuova(ExpenseCategory.Lodging, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule),
            Utente);

        var aggiornata = await _service.UpdateAsync(
            creata.Id,
            Nuova(scelta: null, proposta: ExpenseCategory.Lodging, provenienza: ExpenseCategorySource.MerchantRule),
            Utente);

        Assert.Null(aggiornata.Category);
        Assert.Null(aggiornata.CategorySource);
        Assert.Null(aggiornata.CategoryConfidence);

        // La proposta scartata resta registrata, come per lo smistamento dei ticket.
        Assert.Equal(ExpenseCategory.Lodging, aggiornata.CategorySuggested);
    }

    [Fact]
    public async Task Togliere_la_tipologia_non_la_fa_tornare_dal_documento()
    {
        // La maschera a documento singolo modifica la testata, mentre il documento conserva quello
        // che aveva letto l'OCR. Se il documento potesse riempire una testata svuotata, togliere
        // una tipologia sbagliata non funzionerebbe: sparirebbe dal modulo e tornerebbe al salvataggio.
        var conTipologia = Nuova(ExpenseCategory.Lodging, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule);
        conTipologia.Documents = new List<ExpenseReceiptDocumentDTO>
        {
            new()
            {
                SortOrder = 0,
                MerchantName = "Hotel Roma",
                TotalAmount = 42m,
                Currency = "EUR",
                TransactionDate = DateTime.Today,
                Category = ExpenseCategory.Lodging,
                CategorySource = ExpenseCategorySource.MerchantRule
            }
        };

        var creata = await _service.CreateAsync(conTipologia, Utente);

        var senzaTipologia = Nuova(scelta: null, proposta: ExpenseCategory.Lodging, provenienza: ExpenseCategorySource.MerchantRule);
        senzaTipologia.Documents = creata.Documents;

        var aggiornata = await _service.UpdateAsync(creata.Id, senzaTipologia, Utente);

        Assert.Null(aggiornata.Category);
        Assert.Null(Assert.Single(aggiornata.Documents).Category);
    }

    [Fact]
    public async Task Il_riepilogo_conta_a_parte_le_spese_senza_tipologia()
    {
        await _service.CreateAsync(Nuova(ExpenseCategory.Lodging, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule), Utente);
        await _service.CreateAsync(Nuova(scelta: null, proposta: null, provenienza: null), Utente);

        var riepilogo = await _service.GetSummaryAsync(new ExpenseReceiptFilter(), restrictToUserId: null);

        Assert.Equal(1, riepilogo.MissingCategoryCount);
        Assert.Equal(2, riepilogo.ByCategory.Count);

        // La voce senza tipologia c'e' e sta in fondo: nasconderla darebbe uno spaccato che non
        // somma al totale.
        Assert.Null(riepilogo.ByCategory.Last().Category);
    }

    [Fact]
    public async Task Il_filtro_trova_le_spese_da_classificare()
    {
        await _service.CreateAsync(Nuova(ExpenseCategory.Lodging, ExpenseCategory.Lodging, ExpenseCategorySource.MerchantRule), Utente);
        await _service.CreateAsync(Nuova(scelta: null, proposta: null, provenienza: null), Utente);

        var (senzaTipologia, _) = await _service.SearchAsync(
            new ExpenseReceiptFilter { MissingCategory = true }, restrictToUserId: null);

        var (soloAlloggio, _) = await _service.SearchAsync(
            new ExpenseReceiptFilter { Category = ExpenseCategory.Lodging }, restrictToUserId: null);

        Assert.Single(senzaTipologia);
        Assert.Single(soloAlloggio);
    }
}
