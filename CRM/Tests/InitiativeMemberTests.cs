using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Tests;

/// <summary>
/// Membri e presenze di un'iniziativa. Sono la meta' che risponde a "dov'e' questa persona": se un
/// membro non ha presenze non compare in agenda, e l'elenco diventa una lista decorativa.
/// </summary>
public class InitiativeMemberTests
{
    [Fact]
    public async Task Un_nuovo_membro_riceve_una_presenza_su_tutto_il_periodo()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var response = await ctx.Service.PostAsync(
            InitiativeTestContext.Nuova(giorni: 4, membri: InitiativeTestContext.Utente));

        Assert.True(response.State);

        var id = response.Data!.Id;
        var presenza = Assert.Single(ctx.Presenze(id));

        Assert.Equal(InitiativeTestContext.Utente, presenza.IdUser);
        Assert.Equal(InitiativeScheduleType.Presence, presenza.Type);
        Assert.Equal(DateTime.Today, presenza.Start);

        // L'ultimo giorno va incluso per intero: chiudere a mezzanotte lo taglierebbe fuori.
        Assert.Equal(DateTime.Today.AddDays(3), presenza.End.Date);
    }

    [Fact]
    public async Task Salvare_di_nuovo_non_duplica_le_presenze()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(
            InitiativeTestContext.Nuova(membri: InitiativeTestContext.Utente));

        var id = creata.Data!.Id;
        var daRisalvare = ctx.Ricarica(id);

        await ctx.Service.PostAsync(daRisalvare);

        Assert.Single(ctx.Presenze(id));
    }

    [Fact]
    public async Task Il_ruolo_scelto_sopravvive_ai_salvataggi_successivi()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var iniziativa = InitiativeTestContext.Nuova(membri: InitiativeTestContext.Utente);
        iniziativa.Members.First().Role = InitiativeMemberRole.Technical;

        var creata = await ctx.Service.PostAsync(iniziativa);
        var id = creata.Data!.Id;

        var daRisalvare = ctx.Ricarica(id);
        daRisalvare.Name = "Nome cambiato";
        await ctx.Service.PostAsync(daRisalvare);

        var membro = Assert.Single(ctx.Ricarica(id).Members);
        Assert.Equal(InitiativeMemberRole.Technical, membro.Role);
    }

    [Fact]
    public async Task Chi_e_gia_membro_conserva_quando_e_stato_aggiunto()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(
            InitiativeTestContext.Nuova(membri: InitiativeTestContext.Utente));

        var id = creata.Data!.Id;
        var primoInserimento = ctx.Ricarica(id).Members.Single().AddedAt;

        var conNuovoMembro = ctx.Ricarica(id);
        conNuovoMembro.Members.Add(new InitiativeMember { IdUser = InitiativeTestContext.Collega });
        await ctx.Service.PostAsync(conNuovoMembro);

        var storico = ctx.Ricarica(id).Members.Single(m => m.IdUser == InitiativeTestContext.Utente);
        Assert.Equal(primoInserimento, storico.AddedAt);
    }

    [Fact]
    public async Task Togliere_un_membro_ne_toglie_anche_le_presenze()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(
            membri: new[] { InitiativeTestContext.Utente, InitiativeTestContext.Collega }));

        var id = creata.Data!.Id;
        Assert.Equal(2, ctx.Presenze(id).Count);

        var senzaCollega = ctx.Ricarica(id);
        senzaCollega.Members = senzaCollega.Members
            .Where(m => m.IdUser != InitiativeTestContext.Collega)
            .ToList();

        await ctx.Service.PostAsync(senzaCollega);

        var presenza = Assert.Single(ctx.Presenze(id));
        Assert.Equal(InitiativeTestContext.Utente, presenza.IdUser);
    }

    [Fact]
    public async Task Una_presenza_sovrapposta_alla_stessa_persona_viene_rifiutata()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(giorni: 4));
        var id = creata.Data!.Id;

        var prima = await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Utente,
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(2)
        });

        Assert.True(prima.State);

        var sovrapposta = await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Utente,
            Start = DateTime.Today.AddDays(1),
            End = DateTime.Today.AddDays(3)
        });

        Assert.False(sovrapposta.State);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, sovrapposta.Code);
    }

    [Fact]
    public async Task Due_persone_possono_essere_presenti_nello_stesso_momento()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(giorni: 4));
        var id = creata.Data!.Id;

        await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Utente,
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(2)
        });

        var collega = await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Collega,
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(2)
        });

        Assert.True(collega.State);
    }

    [Fact]
    public async Task Una_presenza_fuori_dal_periodo_viene_rifiutata()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(giorni: 2));
        var id = creata.Data!.Id;

        var fuori = await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Utente,
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(5)
        });

        Assert.False(fuori.State);
    }

    [Fact]
    public async Task Registrare_una_presenza_rende_membro_chi_non_lo_era()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();

        var creata = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(giorni: 3));
        var id = creata.Data!.Id;

        Assert.Empty(ctx.Ricarica(id).Members);

        await ctx.Service.SaveScheduleAsync(id, new InitiativeScheduleDTO
        {
            IdUser = InitiativeTestContext.Collega,
            Start = DateTime.Today,
            End = DateTime.Today.AddDays(1)
        });

        var membro = Assert.Single(ctx.Ricarica(id).Members);
        Assert.Equal(InitiativeTestContext.Collega, membro.IdUser);
    }
}
