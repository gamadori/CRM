using CRM.Client.Services;
using CRM.Server.Data;
using CRM.Server.Services;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace CRM.Tests;

/// <summary>
/// Banco di prova per le iniziative (fiere e trasferte): contesto EF in memoria isolato per test e
/// servizi di contorno sostituiti.
/// </summary>
public sealed class InitiativeTestContext : IDisposable
{
    public const string Utente = "utente-corrente";
    public const string Collega = "collega";

    public ApplicationDbContext Db { get; }
    public InitiativesService Service { get; }

    public InitiativeTestContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"crm-initiative-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Db = new ApplicationDbContext(options);

        var permits = Substitute.For<IPermitsService>();
        permits.IdUser().Returns(Utente);
        permits.IsAdmin().Returns(true);
        permits.GetVisibleCompanyIds().Returns((List<int>?)null);

        Service = new InitiativesService(Db, permits, Substitute.For<ILogEventService>());
    }

    /// <summary>Utenti minimi: l'email e' obbligatoria, senza l'inserimento viene rifiutato.</summary>
    public InitiativeTestContext ConUtenti()
    {
        Db.Users.AddRange(
            new ApplicationUser { Id = Utente, UserName = Utente, Email = $"{Utente}@test.local" },
            new ApplicationUser { Id = Collega, UserName = Collega, Email = $"{Collega}@test.local" });

        Db.SaveChanges();
        return this;
    }

    public static Initiative Nuova(
        InitiativeKind kind = InitiativeKind.Fair,
        int giorni = 3,
        params string[] membri)
    {
        var initiative = new Initiative
        {
            Name = kind == InitiativeKind.Fair ? "Fiera di prova" : "Giro di prova",
            Kind = kind,
            State = InitiativeState.Planned,
            DateFrom = DateTime.Today,
            DateTo = DateTime.Today.AddDays(giorni - 1),
            IdOwner = Utente
        };

        foreach (var idUser in membri)
            initiative.Members.Add(new InitiativeMember { IdUser = idUser });

        return initiative;
    }

    public Initiative Ricarica(int id)
        => Db.Initiatives.Include(x => x.Members).AsNoTracking().Single(x => x.Id == id);

    public List<InitiativeSchedule> Presenze(int id)
        => Db.InitiativeSchedules.AsNoTracking().Where(x => x.IdInitiative == id).ToList();

    public void Dispose() => Db.Dispose();
}
