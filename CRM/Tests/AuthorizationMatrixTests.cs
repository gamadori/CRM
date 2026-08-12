using System.Net;
using System.Net.Http.Json;
using CRM.Shared;

namespace CRM.Tests;

/// <summary>
/// Matrice endpoint × ruolo, provata sull'applicazione vera invece che rileggendo gli attributi.
/// <para>
/// La differenza non e' accademica: <see cref="PublicApiSurfaceTests"/> verifica cosa il codice
/// <b>dichiara</b>, qui si verifica cosa il server <b>risponde</b>. Un criterio registrato con il
/// nome sbagliato, un attributo che non arriva mai in pipeline, una regola globale rimossa per
/// sbaglio: sono tutti casi in cui gli attributi restano perfetti e il perimetro non c'e' piu'.
/// </para>
/// <para>
/// I ruoli sono quelli di <see cref="eRoles"/>. Il criterio "AdminRole" ammette il solo Admin,
/// quindi ogni altro ruolo su un endpoint di impostazioni deve prendere un 403 - non un 200 con i
/// dati, e nemmeno un 401 che confonderebbe "non ti conosco" con "non ti spetta".
/// </para>
/// </summary>
public class AuthorizationMatrixTests : IClassFixture<CrmApiFactory>
{
    private readonly CrmApiFactory _fabbrica;

    public AuthorizationMatrixTests(CrmApiFactory fabbrica) => _fabbrica = fabbrica;

    /// <summary>Gli endpoint che maneggiano segreti o configurazione di sistema.</summary>
    public static TheoryData<string> SoloAdmin() => new()
    {
        "/api/SmtpSettings",
        "/api/SmtpSettings/list",
        "/api/EmailInbox",
        "/api/EmailInbox/list",
        "/api/ApiKeys"
    };

    public static TheoryData<string, string> SoloAdminPerOgniAltroRuolo()
    {
        var dati = new TheoryData<string, string>();

        foreach (var percorso in new[] { "/api/SmtpSettings", "/api/SmtpSettings/list", "/api/EmailInbox", "/api/EmailInbox/list", "/api/ApiKeys" })
        {
            foreach (var ruolo in new[] { nameof(eRoles.SuperUser), nameof(eRoles.Standard), nameof(eRoles.Client) })
                dati.Add(percorso, ruolo);
        }

        return dati;
    }

    [Theory]
    [MemberData(nameof(SoloAdminPerOgniAltroRuolo))]
    public async Task Un_utente_non_amministratore_non_arriva_ai_segreti(string percorso, string ruolo)
    {
        var client = _fabbrica.ClientCome(ruolo);

        var risposta = await client.GetAsync(percorso);

        Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);

        // E non e' che risponde 403 con i dati dentro: il corpo non deve contenere niente.
        var corpo = await risposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(SoloAdmin))]
    public async Task L_amministratore_invece_entra(string percorso)
    {
        var client = _fabbrica.ClientCome(nameof(eRoles.Admin));

        var risposta = await client.GetAsync(percorso);

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Theory]
    [MemberData(nameof(SoloAdmin))]
    public async Task Senza_login_non_si_entra_da_nessuna_parte(string percorso)
    {
        var client = _fabbrica.ClientCome(ruolo: null);

        var risposta = await client.GetAsync(percorso);

        // 401, non 403: qui il server non sa chi sei, ed e' una risposta diversa da "non ti spetta".
        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Theory]
    [InlineData("/api/Quotes/list")]
    [InlineData("/api/Orders/list")]
    [InlineData("/api/Invoices/list")]
    [InlineData("/api/Deals/list")]
    [InlineData("/api/Contacts")]
    [InlineData("/api/Roles")]
    [InlineData("/api/Leads")]
    [InlineData("/api/GlobalSettings")]
    public async Task Gli_endpoint_che_rispondevano_a_chiunque_ora_chiedono_il_login(string percorso)
    {
        // Sono gli endpoint che il 2026-07-22 rispondevano 200 con dati veri senza login. La regola
        // globale (MapControllers().RequireAuthorization()) e' quella che li ha chiusi tutti
        // insieme: questo test e' la sua rete, perche' toglierla non romperebbe nessun attributo.
        var client = _fabbrica.ClientCome(ruolo: null);

        var risposta = await client.GetAsync(percorso);

        Assert.Equal(HttpStatusCode.Unauthorized, risposta.StatusCode);
    }

    [Fact]
    public async Task La_scrittura_delle_impostazioni_generali_resta_agli_amministratori()
    {
        var impostazioni = new GlobalSetting { Id = 1 };

        foreach (var ruolo in new[] { nameof(eRoles.SuperUser), nameof(eRoles.Standard), nameof(eRoles.Client) })
        {
            var client = _fabbrica.ClientCome(ruolo);

            var risposta = await client.PutAsJsonAsync("/api/GlobalSettings/1", impostazioni);

            Assert.Equal(HttpStatusCode.Forbidden, risposta.StatusCode);
        }
    }

    [Fact]
    public async Task Le_impostazioni_generali_si_leggono_anche_senza_essere_amministratori()
    {
        // In lettura servono a tutti: da li' l'applicazione sa com'e' configurata. La differenza
        // fra lettura e scrittura e' voluta, e questo test la tiene ferma - se un giorno qualcuno
        // chiudesse anche la lettura, l'applicazione smetterebbe di funzionare per i non-admin.
        var client = _fabbrica.ClientCome(nameof(eRoles.Standard));

        var risposta = await client.GetAsync("/api/GlobalSettings");

        Assert.Equal(HttpStatusCode.OK, risposta.StatusCode);
    }

    [Fact]
    public async Task Gli_endpoint_dichiarati_pubblici_rispondono_davvero_senza_login()
    {
        // Se anche questo tornasse 401, vorrebbe dire che la regola globale ha inghiottito la
        // superficie pubblica: i webhook e le pagine di firma smetterebbero di funzionare, e il
        // sintomo arriverebbe da fuori (un provider che non consegna piu' la posta).
        var client = _fabbrica.ClientCome(ruolo: null);

        var risposta = await client.GetAsync("/api/Licenses/public-key");

        Assert.NotEqual(HttpStatusCode.Unauthorized, risposta.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, risposta.StatusCode);
    }
}
