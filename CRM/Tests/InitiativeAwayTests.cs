using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Tests;

/// <summary>
/// "Dov'e' questa persona". Si risponde dalle PRESENZE e non dai membri: fare parte della squadra
/// di una fiera non vuol dire esserci tutti i giorni, ed e' proprio la differenza che rende la
/// risposta credibile a chi deve assegnare un ticket adesso.
/// </summary>
public class InitiativeAwayTests
{
    private static async Task<int> CreaAsync(InitiativeTestContext ctx, params string[] membri)
    {
        var response = await ctx.Service.PostAsync(
            InitiativeTestContext.Nuova(InitiativeKind.Fair, giorni: 4, membri: membri));

        return response.Data!.Id;
    }

    [Fact]
    public async Task Chi_ha_una_presenza_nel_periodo_risulta_fuori()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        await CreaAsync(ctx, InitiativeTestContext.Utente);

        var fuori = await ctx.Service.GetAwayUsersAsync(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

        var info = Assert.Single(fuori);
        Assert.Equal(InitiativeTestContext.Utente, info.IdUser);
        Assert.Equal(InitiativeKind.Fair, info.Kind);
    }

    [Fact]
    public async Task Chi_non_partecipa_non_risulta_fuori()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        await CreaAsync(ctx, InitiativeTestContext.Utente);

        var fuori = await ctx.Service.GetAwayUsersAsync(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

        Assert.DoesNotContain(fuori, x => x.IdUser == InitiativeTestContext.Collega);
    }

    [Fact]
    public async Task Fuori_dal_periodo_della_presenza_la_persona_e_disponibile()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx);

        // Presente solo il primo giorno: il terzo e' di nuovo assegnabile.
        await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Utente,
            Start = DateTime.Today,
            End = DateTime.Today.AddHours(18)
        });

        var terzoGiorno = DateTime.Today.AddDays(2);
        var fuori = await ctx.Service.GetAwayUsersAsync(terzoGiorno, terzoGiorno.AddDays(1).AddSeconds(-1));

        Assert.Empty(fuori);
    }

    [Fact]
    public async Task Un_iniziativa_annullata_non_occupa_nessuno()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaAsync(ctx, InitiativeTestContext.Utente);

        var annullata = ctx.Ricarica(id);
        annullata.State = InitiativeState.Cancelled;
        await ctx.Service.PostAsync(annullata);

        var fuori = await ctx.Service.GetAwayUsersAsync(DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1));

        Assert.Empty(fuori);
    }
}
