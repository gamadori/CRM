using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Server.Services.TicketRouting;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Smistamento automatico dei ticket verso i gruppi. Il modello e' sostituito: qui si verificano
/// le regole che devono restare vere comunque risponda l'AI — soglia, candidati, ripiego e
/// tracciamento dell'esito.
/// </summary>
public class TicketRoutingTests : IDisposable
{
    private const int GruppoElettrico = 1;
    private const int GruppoMeccanico = 2;
    private const int GruppoSoftware = 3;

    private const int TipoAssistenza = 10;
    private const int TipoAltro = 11;
    private const int TipoSenzaGruppi = 12;

    private const int Azienda = 1;

    private readonly ApplicationDbContext _db;
    private readonly ITicketRoutingAiClient _ai = Substitute.For<ITicketRoutingAiClient>();
    private readonly ILogEventService _log = Substitute.For<ILogEventService>();
    private readonly TicketRoutingService _service;

    public TicketRoutingTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"routing-test-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options);

        var tipoAssistenza = new TicketType { Id = TipoAssistenza, Desc = "Assistenza" };
        var tipoAltro = new TicketType { Id = TipoAltro, Desc = "Altro" };

        _db.TicketTypes.AddRange(tipoAssistenza, tipoAltro, new TicketType { Id = TipoSenzaGruppi, Desc = "Senza gruppi" });

        // Azienda del ticket: la relazione e' obbligatoria, quindi senza il principale il ticket
        // non verrebbe nemmeno letto dalle query che includono Company.
        _db.Companies.Add(new Company { Id = Azienda, RagioneSociale = "Cliente prova" });

        // Elettrico e Meccanico trattano l'assistenza; Software e' abilitato su un altro tipo.
        _db.Groups.AddRange(
            new Group { Id = GruppoElettrico, Name = "Elettrico", Description = "", AiRoutingHints = "Quadri, sensori, azionamenti", TicketTypes = new List<TicketType> { tipoAssistenza } },
            new Group { Id = GruppoMeccanico, Name = "Meccanico", Description = "", AiRoutingHints = "Cinghie, cuscinetti, usura", TicketTypes = new List<TicketType> { tipoAssistenza } },
            new Group { Id = GruppoSoftware, Name = "Software", Description = "", AiRoutingHints = "Gestionale e licenze", TicketTypes = new List<TicketType> { tipoAltro } });

        _db.SaveChanges();

        _ai.IsAvailable.Returns(true);
        _ai.Model.Returns("modello-test");

        var permits = Substitute.For<IPermitsService>();
        permits.CanGetObject(Arg.Any<int>()).Returns(true);

        _service = new TicketRoutingService(
            _db,
            _ai,
            Substitute.For<CRM.Server.Services.ITicketsService>(),
            permits,
            _log,
            Substitute.For<ILogger<TicketRoutingService>>());
    }

    public void Dispose() => _db.Dispose();

    // ─── Attivazione ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Con_smistamento_disattivato_non_chiama_il_modello()
    {
        Impostazioni(attivo: false);
        var ticket = Ticket();

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.False(esito.Executed);
        Assert.Null(Ricarica(ticket.Id).IdGroupAssigned);
        await _ai.DidNotReceiveWithAnyArgs().SuggestGroupAsync(default!, default, default);
    }

    [Fact]
    public async Task I_ticket_da_email_si_possono_escludere_dallo_smistamento()
    {
        Impostazioni(applicaAEmail: false);
        var ticket = Ticket();

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.InboundEmail);

        Assert.False(esito.Executed);
        await _ai.DidNotReceiveWithAnyArgs().SuggestGroupAsync(default!, default, default);
    }

    [Fact]
    public async Task Un_ticket_gia_assegnato_a_un_gruppo_non_viene_toccato()
    {
        Impostazioni();
        var ticket = Ticket(gruppo: GruppoMeccanico);
        Suggerisce(GruppoElettrico, 0.99);

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.False(esito.Executed);
        Assert.Equal(GruppoMeccanico, Ricarica(ticket.Id).IdGroupAssigned);
    }

    // ─── Soglia ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sopra_la_soglia_il_gruppo_viene_assegnato_da_solo()
    {
        Impostazioni(soglia: 0.75);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.9, "Allarme sul quadro elettrico");

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var salvato = Ricarica(ticket.Id);
        Assert.True(esito.Executed);
        Assert.Equal(GruppoElettrico, salvato.IdGroupAssigned);
        Assert.Equal(GruppoElettrico, salvato.AiSuggestedGroupId);
        Assert.True(salvato.AiRoutingApplied);
        Assert.Equal(AiRoutingOutcome.Accepted, salvato.AiRoutingOutcome);
        Assert.Equal("Allarme sul quadro elettrico", salvato.AiRoutingReason);
        Assert.NotNull(salvato.AiRoutedAt);
    }

    [Fact]
    public async Task Sotto_la_soglia_resta_un_suggerimento_e_il_ticket_resta_in_coda()
    {
        Impostazioni(soglia: 0.75);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.6);

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var salvato = Ricarica(ticket.Id);
        Assert.True(esito.Executed);
        Assert.Null(salvato.IdGroupAssigned);
        Assert.Equal(GruppoElettrico, salvato.AiSuggestedGroupId);
        Assert.False(salvato.AiRoutingApplied);
        Assert.Equal(AiRoutingOutcome.Pending, salvato.AiRoutingOutcome);
    }

    [Fact]
    public async Task La_confidenza_esattamente_pari_alla_soglia_assegna()
    {
        Impostazioni(soglia: 0.75);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.75);

        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.Equal(GruppoElettrico, Ricarica(ticket.Id).IdGroupAssigned);
    }

    // ─── Candidati ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Al_modello_arrivano_solo_i_gruppi_abilitati_al_tipo_di_ticket()
    {
        Impostazioni(filtraPerTipo: true);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.9);

        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var richiesta = RichiestaInviata();
        Assert.Equal(new[] { "Elettrico", "Meccanico" }, richiesta.Candidates.Select(c => c.Name).ToArray());
    }

    [Fact]
    public async Task Senza_filtro_per_tipo_il_modello_vede_tutti_i_gruppi()
    {
        Impostazioni(filtraPerTipo: false);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.9);

        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.Equal(3, RichiestaInviata().Candidates.Count);
    }

    [Fact]
    public async Task Senza_gruppi_candidati_il_modello_non_viene_nemmeno_chiamato()
    {
        Impostazioni(filtraPerTipo: true);
        var ticket = Ticket(tipo: TipoSenzaGruppi);

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.True(esito.Executed);
        Assert.Null(Ricarica(ticket.Id).IdGroupAssigned);
        await _ai.DidNotReceiveWithAnyArgs().SuggestGroupAsync(default!, default, default);
    }

    // ─── Ripiego ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Se_l_AI_non_decide_entra_in_gioco_il_gruppo_di_ripiego()
    {
        Impostazioni(ripiego: GruppoMeccanico);
        var ticket = Ticket();
        Suggerisce(null, 0.2, "Richiesta troppo vaga");

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var salvato = Ricarica(ticket.Id);
        Assert.Equal(GruppoMeccanico, salvato.IdGroupAssigned);
        Assert.Null(salvato.AiSuggestedGroupId);
        Assert.False(salvato.AiRoutingApplied);
        Assert.Equal(AiRoutingOutcome.None, salvato.AiRoutingOutcome);
        Assert.Equal("Richiesta troppo vaga", salvato.AiRoutingReason);
        Assert.Equal(GruppoMeccanico, esito.AssignedGroupId);
    }

    [Fact]
    public async Task Senza_ripiego_il_ticket_resta_nella_coda_generale()
    {
        Impostazioni();
        var ticket = Ticket();
        Suggerisce(null, 0);

        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.Null(Ricarica(ticket.Id).IdGroupAssigned);
    }

    [Fact]
    public async Task Un_errore_del_modello_non_impedisce_la_vita_del_ticket()
    {
        Impostazioni();
        var ticket = Ticket();
        _ai.SuggestGroupAsync(Arg.Any<TicketRoutingRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<TicketRoutingSuggestion?>(_ => throw new InvalidOperationException("modello non raggiungibile"));

        var esito = await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        Assert.False(esito.Executed);
        Assert.Null(Ricarica(ticket.Id).IdGroupAssigned);
    }

    // ─── Decisione dell'operatore ────────────────────────────────────────────

    [Fact]
    public async Task Accettare_il_suggerimento_assegna_il_gruppo_e_registra_l_esito()
    {
        Impostazioni(soglia: 0.9);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.6);
        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var risposta = await _service.AcceptSuggestionAsync(ticket.Id);

        var salvato = Ricarica(ticket.Id);
        Assert.True(risposta.State);
        Assert.Equal(GruppoElettrico, salvato.IdGroupAssigned);
        Assert.Equal(AiRoutingOutcome.Accepted, salvato.AiRoutingOutcome);
        // Resta "non applicato": il gruppo lo ha scelto una persona, non l'automatismo.
        Assert.False(salvato.AiRoutingApplied);
    }

    [Fact]
    public async Task Scartare_il_suggerimento_lascia_il_ticket_senza_gruppo()
    {
        Impostazioni(soglia: 0.9);
        var ticket = Ticket();
        Suggerisce(GruppoElettrico, 0.6);
        await _service.RouteAsync(ticket.Id, TicketRoutingSource.Application);

        var risposta = await _service.DismissSuggestionAsync(ticket.Id);

        var salvato = Ricarica(ticket.Id);
        Assert.True(risposta.State);
        Assert.Null(salvato.IdGroupAssigned);
        Assert.Equal(AiRoutingOutcome.Dismissed, salvato.AiRoutingOutcome);
        // Il suggerimento resta registrato: serve alle statistiche.
        Assert.Equal(GruppoElettrico, salvato.AiSuggestedGroupId);
    }

    [Fact]
    public async Task Senza_suggerimento_non_c_e_niente_da_accettare()
    {
        Impostazioni();
        var ticket = Ticket();

        var risposta = await _service.AcceptSuggestionAsync(ticket.Id);

        Assert.False(risposta.State);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, risposta.Code);
    }

    // ─── Esito dopo una modifica manuale ─────────────────────────────────────

    [Theory]
    [InlineData(AiRoutingOutcome.Accepted, GruppoElettrico, GruppoElettrico, AiRoutingOutcome.Accepted)]
    [InlineData(AiRoutingOutcome.Accepted, GruppoElettrico, GruppoMeccanico, AiRoutingOutcome.Corrected)]
    [InlineData(AiRoutingOutcome.Pending, GruppoElettrico, GruppoElettrico, AiRoutingOutcome.Accepted)]
    [InlineData(AiRoutingOutcome.Pending, GruppoElettrico, GruppoMeccanico, AiRoutingOutcome.Corrected)]
    public void Il_cambio_di_gruppo_registra_conferma_o_correzione(
        AiRoutingOutcome attuale, int suggerito, int nuovo, AiRoutingOutcome atteso)
    {
        Assert.Equal(atteso, TicketRoutingOutcomes.AfterGroupChange(attuale, suggerito, nuovo));
    }

    [Fact]
    public void Togliere_il_gruppo_messo_dall_AI_e_una_correzione()
    {
        Assert.Equal(
            AiRoutingOutcome.Corrected,
            TicketRoutingOutcomes.AfterGroupChange(AiRoutingOutcome.Accepted, GruppoElettrico, null));
    }

    [Fact]
    public void Senza_suggerimento_l_esito_non_cambia()
    {
        Assert.Equal(
            AiRoutingOutcome.None,
            TicketRoutingOutcomes.AfterGroupChange(AiRoutingOutcome.None, null, GruppoMeccanico));
    }

    // ─── Stato per la pagina di configurazione ───────────────────────────────

    [Fact]
    public async Task Lo_stato_conta_i_gruppi_senza_competenze_dichiarate()
    {
        Impostazioni();
        _db.Groups.Add(new Group { Id = 4, Name = "Logistica", Description = "", AiRoutingHints = null });
        await _db.SaveChangesAsync();

        var stato = await _service.GetStatusAsync();

        Assert.Equal(4, stato.GroupsTotal);
        Assert.Equal(3, stato.GroupsWithHints);
        Assert.True(stato.AiAvailable);
    }

    [Fact]
    public async Task L_accuratezza_confronta_conferme_e_correzioni()
    {
        Impostazioni();
        _db.Tickets.AddRange(
            TicketConEsito(101, AiRoutingOutcome.Accepted),
            TicketConEsito(102, AiRoutingOutcome.Accepted),
            TicketConEsito(103, AiRoutingOutcome.Accepted),
            TicketConEsito(104, AiRoutingOutcome.Corrected),
            TicketConEsito(105, AiRoutingOutcome.Pending));
        await _db.SaveChangesAsync();

        var stato = await _service.GetStatusAsync();

        Assert.Equal(5, stato.RoutedLast30Days);
        Assert.Equal(0.75, stato.AccuracyLast30Days);
    }

    // ─── Utilita' ────────────────────────────────────────────────────────────

    private void Impostazioni(
        bool attivo = true,
        double soglia = 0.75,
        bool filtraPerTipo = true,
        int? ripiego = null,
        bool applicaAEmail = true)
    {
        _db.TicketRoutingSettings.Add(new TicketRoutingSetting
        {
            Id = TicketRoutingSetting.SingletonId,
            Enabled = attivo,
            AutoAssignThreshold = soglia,
            RestrictToTicketTypeGroups = filtraPerTipo,
            IdFallbackGroup = ripiego,
            ApplyToEmailTickets = applicaAEmail
        });
        _db.SaveChanges();
    }

    private Ticket Ticket(int tipo = TipoAssistenza, int? gruppo = null)
    {
        var ticket = new Ticket
        {
            Id = 1,
            IdType = tipo,
            IdCompany = Azienda,
            IdGroupAssigned = gruppo,
            IdUserOpened = "operatore",
            DateOpened = DateTime.Now,
            Description = "La macchina si ferma con allarme sul quadro e non riparte.",
            Numero = string.Empty,
            CloseDescription = string.Empty,
            CloseNote = string.Empty
        };

        _db.Tickets.Add(ticket);
        _db.SaveChanges();
        _db.Entry(ticket).State = EntityState.Detached;

        return ticket;
    }

    private static Ticket TicketConEsito(int id, AiRoutingOutcome esito) => new()
    {
        Id = id,
        IdType = TipoAssistenza,
        IdCompany = Azienda,
        IdUserOpened = "operatore",
        DateOpened = DateTime.Now,
        Description = string.Empty,
        Numero = string.Empty,
        CloseDescription = string.Empty,
        CloseNote = string.Empty,
        AiRoutedAt = DateTime.Now.AddDays(-1),
        AiSuggestedGroupId = GruppoElettrico,
        AiRoutingOutcome = esito,
        AiRoutingApplied = esito == AiRoutingOutcome.Accepted
    };

    private void Suggerisce(int? gruppo, double confidenza, string? motivo = "motivo")
        => _ai.SuggestGroupAsync(Arg.Any<TicketRoutingRequest>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new TicketRoutingSuggestion(gruppo, confidenza, motivo));

    private TicketRoutingRequest RichiestaInviata()
        => (TicketRoutingRequest)_ai.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(ITicketRoutingAiClient.SuggestGroupAsync)).GetArguments()[0]!;

    private Ticket Ricarica(int id) => _db.Tickets.AsNoTracking().Single(t => t.Id == id);
}
