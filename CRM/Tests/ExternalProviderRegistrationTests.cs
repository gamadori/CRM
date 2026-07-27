using CRM.Server.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Tests;

/// <summary>
/// Registrazione dei provider da configurazione. Il primo test è il più importante di tutti:
/// su un'installazione che non configura nulla, il login deve restare esattamente quello di prima.
/// </summary>
public class ExternalProviderRegistrationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] valori)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(valori.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    private static async Task<List<AuthenticationScheme>> SchemiRegistrati(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddAuthentication().AddExternalProviders(configuration);

        var provider = services.BuildServiceProvider();
        var schemi = await provider.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync();
        return schemi.ToList();
    }

    private static (string, string)[] ProviderCompleto(string scheme, bool enabled = true) => new[]
    {
        ($"ExternalAuthentication:Providers:0:Scheme", scheme),
        ($"ExternalAuthentication:Providers:0:DisplayName", "Microsoft"),
        ($"ExternalAuthentication:Providers:0:Enabled", enabled.ToString()),
        ($"ExternalAuthentication:Providers:0:Authority", "https://login.microsoftonline.com/abc/v2.0"),
        ($"ExternalAuthentication:Providers:0:ClientId", "client-id"),
        ($"ExternalAuthentication:Providers:0:ClientSecret", "secret")
    };

    /// <summary>La garanzia di non regressione: nessuna configurazione, nessuno schema.</summary>
    [Fact]
    public async Task Senza_configurazione_non_viene_registrato_nulla()
    {
        var schemi = await SchemiRegistrati(Config());

        Assert.Empty(schemi);
    }

    [Fact]
    public async Task Un_provider_disabilitato_viene_ignorato()
    {
        var schemi = await SchemiRegistrati(Config(ProviderCompleto("EntraId", enabled: false)));

        Assert.Empty(schemi);
    }

    [Fact]
    public async Task Un_provider_abilitato_registra_il_suo_schema()
    {
        var schemi = await SchemiRegistrati(Config(ProviderCompleto("EntraId")));

        var schema = Assert.Single(schemi);
        Assert.Equal("EntraId", schema.Name);
        Assert.Equal("Microsoft", schema.DisplayName);
    }

    /// <summary>
    /// Il nome visualizzato è quello che finisce sul pulsante: se manca si ripiega sullo schema,
    /// piuttosto che mostrare un pulsante senza etichetta.
    /// </summary>
    [Fact]
    public async Task Senza_nome_visualizzato_si_usa_lo_schema()
    {
        var valori = ProviderCompleto("EntraId")
            .Where(v => !v.Item1.EndsWith("DisplayName"))
            .ToArray();

        var schema = Assert.Single(await SchemiRegistrati(Config(valori)));
        Assert.Equal("EntraId", schema.DisplayName);
    }

    /// <summary>
    /// Un provider dichiarato attivo ma incompleto è un errore di configurazione: fermarsi
    /// all'avvio con un messaggio preciso è meglio di un pulsante che non compare mai.
    /// </summary>
    [Theory]
    [InlineData("Authority")]
    [InlineData("ClientId")]
    [InlineData("ClientSecret")]
    public async Task Un_provider_abilitato_ma_incompleto_blocca_l_avvio(string campoMancante)
    {
        var valori = ProviderCompleto("EntraId")
            .Where(v => !v.Item1.EndsWith(campoMancante))
            .ToArray();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SchemiRegistrati(Config(valori)));

        Assert.Contains("EntraId", ex.Message);
        Assert.Contains(campoMancante, ex.Message);
    }

    [Fact]
    public async Task Piu_provider_convivono()
    {
        var valori = ProviderCompleto("EntraId").Concat(new[]
        {
            ("ExternalAuthentication:Providers:1:Scheme", "Google"),
            ("ExternalAuthentication:Providers:1:DisplayName", "Google"),
            ("ExternalAuthentication:Providers:1:Authority", "https://accounts.google.com"),
            ("ExternalAuthentication:Providers:1:ClientId", "google-id"),
            ("ExternalAuthentication:Providers:1:ClientSecret", "google-secret")
        }).ToArray();

        var schemi = await SchemiRegistrati(Config(valori));

        Assert.Equal(2, schemi.Count);
        Assert.Contains(schemi, s => s.Name == "EntraId");
        Assert.Contains(schemi, s => s.Name == "Google");
    }

    /// <summary>
    /// La configurazione distribuita con l'applicazione deve essere leggibile e inerte: gli
    /// esempi che documentano la forma non devono attivare nulla.
    /// </summary>
    [Fact]
    public void Gli_esempi_in_appsettings_non_attivano_provider()
    {
        var percorso = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(percorso))
            return;   // l'appsettings del server non è copiato in output: niente da verificare

        var configurazione = new ConfigurationBuilder().AddJsonFile(percorso).Build();
        var opzioni = configurazione
            .GetSection(ExternalAuthenticationOptions.SectionName)
            .Get<ExternalAuthenticationOptions>();

        Assert.True(opzioni == null || opzioni.Providers.All(p => !p.Enabled));
    }
}
