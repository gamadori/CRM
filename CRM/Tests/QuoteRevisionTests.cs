using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Revisioni del preventivo. La regola concordata e' che una revisione <b>non e' un documento
/// nuovo</b>: per il cliente resta lo stesso preventivo, stesso numero, con un progressivo di
/// revisione. Da qui discendono le due cose che i test difendono.
/// <para>
/// Primo: la versione precedente resta consultabile con il suo stato e il suo PDF, ma smette di
/// essere quella corrente. Se si potesse revisionare una versione gia' superata, esisterebbero due
/// "correnti" sullo stesso numero e nessuno saprebbe piu' quale ha in mano il cliente.
/// </para>
/// <para>
/// Secondo: si revisiona finche' il documento e' solo una proposta. Accettato, o gia' diventato
/// ordine, non si tocca piu': le variazioni si fanno a valle, altrimenti il preventivo direbbe una
/// cosa diversa dall'ordine che ne e' nato.
/// </para>
/// </summary>
public class QuoteRevisionTests : IDisposable
{
    private const string Utente = "utente-corrente";

    private readonly ApplicationDbContext _db;
    private readonly QuotesService _servizio;

    public QuoteRevisionTests()
    {
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-quote-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        var permessi = Substitute.For<IPermitsService>();
        permessi.IdUser().Returns(Utente);
        permessi.IsAdmin().Returns(true);
        permessi.GetVisibleCompanyIds().Returns((List<int>?)null);

        _servizio = new QuotesService(
            _db,
            Substitute.For<UserManager<ApplicationUser>>(
                Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null),
            Substitute.For<IHttpContextAccessor>(),
            permessi,
            Substitute.For<ILogEventService>(),
            Substitute.For<IQuotePdfGenerator>(),
            Substitute.For<IOrdersService>(),
            Substitute.For<IEmailSenderPlus>(),
            Substitute.For<IWebHostEnvironment>());
    }

    private Quote Preventivo(QuoteStates stato = QuoteStates.Sent, bool corrente = true, int revisione = 0)
    {
        var quote = new Quote
        {
            Number = "PR-2026-0007",
            Revision = revisione,
            IsCurrent = corrente,
            State = stato,
            Date = DateTime.Today,
            IdCompany = 1,
            IdUser = Utente,
            Rows =
            {
                new QuoteRow { Description = "Righa A", Quantity = 2, UnitPrice = 50m, VatRate = 22m, SortOrder = 0 },
                new QuoteRow { Description = "Riga B", Quantity = 1, UnitPrice = 100m, DiscountPct = 10m, VatRate = 22m, SortOrder = 1 }
            }
        };

        _db.Quotes.Add(quote);
        _db.SaveChanges();

        return quote;
    }

    [Fact]
    public async Task La_revisione_tiene_il_numero_e_alza_il_progressivo()
    {
        var origine = Preventivo();

        var esito = await _servizio.CreateRevisionAsync(origine.Id);

        Assert.True(esito.State);

        var revisione = _db.Quotes.AsNoTracking().Single(q => q.Id != origine.Id);

        Assert.Equal(origine.Number, revisione.Number);
        Assert.Equal(1, revisione.Revision);
        Assert.Equal(QuoteStates.Draft, revisione.State);
        Assert.Equal(origine.Id, revisione.IdRootQuote);
    }

    [Fact]
    public async Task La_versione_precedente_diventa_storia()
    {
        var origine = Preventivo();

        await _servizio.CreateRevisionAsync(origine.Id);

        var superata = _db.Quotes.AsNoTracking().Single(q => q.Id == origine.Id);

        Assert.False(superata.IsCurrent);
        Assert.NotNull(superata.SupersededAt);

        // ...ma non perde il suo stato: resta il documento che il cliente ha ricevuto.
        Assert.Equal(QuoteStates.Sent, superata.State);

        var corrente = _db.Quotes.AsNoTracking().Single(q => q.IsCurrent);
        Assert.NotEqual(origine.Id, corrente.Id);
    }

    [Fact]
    public async Task La_revisione_copia_le_righe_e_rifa_i_conti()
    {
        var origine = Preventivo();

        await _servizio.CreateRevisionAsync(origine.Id);

        var revisione = _db.Quotes.Include(q => q.Rows).AsNoTracking().Single(q => q.Id != origine.Id);

        Assert.Equal(2, revisione.Rows.Count);

        // 100 (2x50) + 90 (100 meno il 10%) = 190 imponibile, 41.80 di IVA.
        Assert.Equal(190m, revisione.Subtotal);
        Assert.Equal(41.80m, revisione.TotalVat);
        Assert.Equal(231.80m, revisione.Total);
        Assert.Equal(10m, revisione.TotalDiscount);

        // Le righe sono copie: modificare la revisione non deve toccare l'originale.
        Assert.All(revisione.Rows, r => Assert.NotEqual(0, r.Id));
        Assert.DoesNotContain(revisione.Rows, r => origine.Rows.Any(o => o.Id == r.Id));
    }

    [Fact]
    public async Task La_revisione_di_una_revisione_punta_sempre_al_capostipite()
    {
        var origine = Preventivo();

        await _servizio.CreateRevisionAsync(origine.Id);

        var prima = _db.Quotes.AsNoTracking().Single(q => q.IsCurrent);

        // Per revisionarla di nuovo deve uscire dalla bozza: in bozza si modifica e basta.
        var tracciata = _db.Quotes.Single(q => q.Id == prima.Id);
        tracciata.State = QuoteStates.Sent;
        await _db.SaveChangesAsync();

        var esito = await _servizio.CreateRevisionAsync(prima.Id);
        Assert.True(esito.State);

        var seconda = _db.Quotes.AsNoTracking().Single(q => q.IsCurrent);

        Assert.Equal(2, seconda.Revision);
        Assert.Equal(origine.Id, seconda.IdRootQuote);   // non l'id della revisione intermedia
        Assert.Equal(origine.Number, seconda.Number);
    }

    [Fact]
    public async Task Una_versione_gia_superata_non_si_revisiona()
    {
        var superata = Preventivo(corrente: false, revisione: 0);

        var esito = await _servizio.CreateRevisionAsync(superata.Id);

        Assert.False(esito.State);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, esito.Code);
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task Una_bozza_non_si_revisiona_perche_si_modifica_e_basta()
    {
        var bozza = Preventivo(QuoteStates.Draft);

        var esito = await _servizio.CreateRevisionAsync(bozza.Id);

        Assert.False(esito.State);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, esito.Code);
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task Un_preventivo_accettato_non_si_revisiona_si_lavora_sull_ordine()
    {
        var accettato = Preventivo(QuoteStates.Accepted);

        var esito = await _servizio.CreateRevisionAsync(accettato.Id);

        Assert.False(esito.State);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, esito.Code);
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task Con_un_ordine_collegato_il_preventivo_e_immutabile()
    {
        var origine = Preventivo();

        _db.Orders.Add(new Order
        {
            Number = "ORD-2026-0001",
            Date = DateTime.Today,
            IdCompany = origine.IdCompany,
            IdQuote = origine.Id
        });
        await _db.SaveChangesAsync();

        var esito = await _servizio.CreateRevisionAsync(origine.Id);

        Assert.False(esito.State);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, esito.Code);
        Assert.Single(_db.Quotes);
    }

    [Fact]
    public async Task Un_preventivo_che_non_esiste_non_crea_niente()
    {
        var esito = await _servizio.CreateRevisionAsync(4242);

        Assert.False(esito.State);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, esito.Code);
        Assert.Empty(_db.Quotes);
    }

    public void Dispose() => _db.Dispose();
}
