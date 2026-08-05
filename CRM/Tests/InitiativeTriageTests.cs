using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Il triage della sera. Due cose devono reggere: dire cosa manca su un biglietto raccolto in
/// trenta secondi, e riconoscere i clienti che si hanno gia' senza proporre accostamenti campati
/// in aria - un suggerimento sbagliato costa piu' di un suggerimento assente, perche' qualcuno lo
/// accetta.
/// </summary>
public class InitiativeTriageTests
{
    private static Company Azienda(int id, string ragioneSociale, string? email = null) => new()
    {
        Id = id,
        RagioneSociale = ragioneSociale,
        Email = email ?? string.Empty,
        CompanyType = CompanyTypes.Customer,
        Note = string.Empty,
        Logo = string.Empty,
        ResellerName = string.Empty
    };

    private static async Task<int> CreaFieraAsync(InitiativeTestContext ctx)
    {
        var response = await ctx.Service.PostAsync(InitiativeTestContext.Nuova(InitiativeKind.Fair));
        return response.Data!.Id;
    }

    [Fact]
    public async Task Dice_cosa_manca_sul_biglietto()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Leads.Add(new Lead { Name = "Mario Rossi", IdInitiative = id, Email = "mario@acme.it", CompanyName = "Acme" });
        ctx.Db.SaveChanges();

        var triage = await ctx.Service.GetLeadTriageAsync(id);
        var lead = Assert.Single(triage);

        // Ha nome, recapito e azienda: manca solo la cosa che nessuno ricostruisce a posteriori.
        Assert.Equal(new[] { "cosa voleva" }, lead.Missing);
        Assert.True(lead.IsIncomplete);
    }

    [Fact]
    public async Task Il_segnaposto_della_cattura_rapida_viene_segnalato_come_nome_mancante()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Leads.Add(new Lead
        {
            Name = "Biglietto delle 14:32",
            IdInitiative = id,
            Phone = "3331234567",
            CompanyName = "Acme",
            Note = "vuole il listino"
        });

        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));
        Assert.Contains("nome", lead.Missing);
    }

    [Fact]
    public async Task Un_biglietto_completo_non_ha_niente_da_completare()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Leads.Add(new Lead
        {
            Name = "Mario Rossi",
            IdInitiative = id,
            Email = "mario@acme.it",
            CompanyName = "Acme",
            Note = "preventivo fresatrice"
        });

        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));
        Assert.False(lead.IsIncomplete);
    }

    [Fact]
    public async Task Riconosce_il_cliente_dall_email_del_contatto()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.Add(Azienda(1, "Acme Industrie"));
        ctx.Db.Contacts.Add(new Contact
        {
            Id = 1,
            IdCompany = 1,
            Name = "Mario",
            Surname = "Rossi",
            Email = "mario@acme.it",
            Phone = string.Empty,
            Mobile = string.Empty,
            Note = string.Empty
        });

        ctx.Db.Leads.Add(new Lead { Name = "M. Rossi", IdInitiative = id, Email = "mario@acme.it" });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));

        Assert.Equal(1, lead.SuggestedCompanyId);
        Assert.Equal("stessa email di un contatto", lead.SuggestionReason);
    }

    [Fact]
    public async Task Riconosce_il_cliente_dal_dominio_aziendale()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.Add(Azienda(1, "Acme Industrie", "info@acme.it"));
        ctx.Db.Leads.Add(new Lead { Name = "Luigi Bianchi", IdInitiative = id, Email = "luigi@acme.it" });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));

        Assert.Equal(1, lead.SuggestedCompanyId);
        Assert.Contains("dominio", lead.SuggestionReason);
    }

    [Fact]
    public async Task Un_dominio_generico_non_e_un_indizio()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        // Due indirizzi gmail non dicono che si tratti della stessa azienda.
        ctx.Db.Companies.Add(Azienda(1, "Acme Industrie", "acme@gmail.com"));
        ctx.Db.Leads.Add(new Lead { Name = "Luigi Bianchi", IdInitiative = id, Email = "luigi@gmail.com" });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));
        Assert.Null(lead.SuggestedCompanyId);
    }

    [Fact]
    public async Task La_forma_giuridica_non_distingue_due_ragioni_sociali()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.Add(Azienda(1, "Muller S.r.l."));
        ctx.Db.Leads.Add(new Lead { Name = "Klaus", IdInitiative = id, CompanyName = "MULLER SRL" });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));

        Assert.Equal(1, lead.SuggestedCompanyId);
        Assert.Equal("ragione sociale corrispondente", lead.SuggestionReason);
    }

    [Fact]
    public async Task Un_nome_ambiguo_fra_piu_aziende_non_propone_nulla()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.AddRange(Azienda(1, "Rossi SRL"), Azienda(2, "Rossi S.p.A."));
        ctx.Db.Leads.Add(new Lead { Name = "Tizio", IdInitiative = id, CompanyName = "Rossi" });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));
        Assert.Null(lead.SuggestedCompanyId);
    }

    [Fact]
    public async Task Chi_e_gia_collegato_non_riceve_proposte()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.Add(Azienda(1, "Acme Industrie", "info@acme.it"));
        ctx.Db.Leads.Add(new Lead { Name = "Luigi", IdInitiative = id, Email = "luigi@acme.it", IdCompany = 1 });
        ctx.Db.SaveChanges();

        var lead = Assert.Single(await ctx.Service.GetLeadTriageAsync(id));
        Assert.Null(lead.SuggestedCompanyId);
    }

    [Fact]
    public async Task Collegare_allinea_la_ragione_sociale_a_quella_dell_anagrafica()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var id = await CreaFieraAsync(ctx);

        ctx.Db.Companies.Add(Azienda(1, "Muller S.r.l."));

        var lead = new Lead { Name = "Klaus", IdInitiative = id, CompanyName = "MULLER SRL" };
        ctx.Db.Leads.Add(lead);
        ctx.Db.SaveChanges();

        Assert.True(await ctx.Service.LinkLeadToCompanyAsync(id, lead.Id, 1));

        var salvato = ctx.Db.Leads.Single(x => x.Id == lead.Id);
        Assert.Equal(1, salvato.IdCompany);
        Assert.Equal("Muller S.r.l.", salvato.CompanyName);
    }

    [Fact]
    public async Task Non_si_collega_un_lead_di_un_altra_iniziativa()
    {
        using var ctx = new InitiativeTestContext().ConUtenti();
        var fiera = await CreaFieraAsync(ctx);
        var altra = (await ctx.Service.PostAsync(InitiativeTestContext.Nuova(InitiativeKind.Fair))).Data!.Id;

        ctx.Db.Companies.Add(Azienda(1, "Acme"));

        var lead = new Lead { Name = "Tizio", IdInitiative = altra };
        ctx.Db.Leads.Add(lead);
        ctx.Db.SaveChanges();

        Assert.False(await ctx.Service.LinkLeadToCompanyAsync(fiera, lead.Id, 1));
    }
}
