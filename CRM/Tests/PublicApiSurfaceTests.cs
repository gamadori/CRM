using System.Reflection;
using CRM.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CRM.Tests;

/// <summary>
/// La superficie pubblica dell'API: quali endpoint rispondono <b>senza login</b>.
/// <para>
/// Dal 2026-07-22 la protezione e' fail-closed (<c>MapControllers().RequireAuthorization()</c>),
/// quindi non serve piu' ricordarsi <c>[Authorize]</c> su ogni controller. Resta pero' il rovescio:
/// basta un <c>[AllowAnonymous]</c> messo per far funzionare qualcosa in fretta e quell'endpoint
/// esce dal perimetro senza che nessuno se ne accorga. Qui l'elenco e' scritto a mano: aggiungere
/// un endpoint anonimo diventa una decisione, non una svista.
/// </para>
/// <para>
/// Se questo test si rompe, la domanda da farsi non e' "come aggiorno l'elenco" ma "questo
/// endpoint puo' davvero rispondere a chiunque, e cosa restituisce a chi non ha fatto login".
/// </para>
/// </summary>
public class PublicApiSurfaceTests
{
    /// <summary>
    /// Gli endpoint che devono restare raggiungibili senza login, ognuno con il motivo per cui
    /// puo' permetterselo: si autentica da solo con un token proprio, oppure non dice niente.
    /// </summary>
    private static readonly string[] SuperficieAttesa =
    {
        // Rilascio e revoca dei token: sono loro a fare l'autenticazione.
        "AuthorizationController.Authorize",
        "AuthorizationController.Exchange",
        "AuthorizationController.Logout",

        // Webhook degli ESP: si autenticano con il token della casella o del provider.
        "EmailInboundController.Receive",
        "EmailWebhooksController.Brevo",
        "EmailWebhooksController.SendGrid",

        // Integrazioni con X-Api-Key: la chiave e' nell'intestazione e vale solo nel suo ambito.
        "ExternalTicketsController.Create",
        "ExternalTicketsController.GetById",
        "ExternalTicketsController.GetList",
        "FieldController.Analyze",
        "FieldController.CreateLead",
        "FieldController.GetInitiatives",
        "FieldController.Ping",
        "MachineParametersController.Download",
        "MachineParametersController.GetArticles",
        "MachineParametersController.GetLatestArticleBackup",
        "MachineParametersController.GetLatestProductBackup",
        "MachineParametersController.UploadArticleBackup",

        // Licenze macchina: si presentano con la loro MachineKey.
        "MachineLicenseController.Pull",
        "MachineLicenseController.Register",

        // Chiavi pubbliche: sono pubbliche per definizione.
        "LicensesController.GetPublicKey",
        "TicketsController.GetVapidPublicKey",

        // Firma da remoto: il cliente non ha un account, il suo fattore e' il link ricevuto.
        // Il link ora scade (vedi SignatureTokenExpiryTests).
        "TicketInterventionsController.ConfirmSignature",
        "TicketInterventionsController.RejectSignature",
        "TicketInterventionsController.RemoteSignatureInfo",
        "TicketInterventionsController.SubmitRemoteSignature"
    };

    [Fact]
    public void Nessun_endpoint_anonimo_oltre_a_quelli_dichiarati()
    {
        var effettiva = Endpoints()
            .Where(e => e.Anonimo)
            .Select(e => e.Nome)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var attesa = SuperficieAttesa.OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(attesa, effettiva);
    }

    [Theory]
    [InlineData(typeof(SmtpSettingsController))]
    [InlineData(typeof(EmailInboxController))]
    [InlineData(typeof(ApiKeysController))]
    public void I_controller_che_maneggiano_segreti_chiedono_il_ruolo_Admin(Type controller)
    {
        // Il login da solo non basta: qui dentro ci sono credenziali della posta e chiavi API,
        // e chi puo' scriverle puo' dirottare quello che entra e quello che esce.
        var autorizzazione = controller.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(autorizzazione);
        Assert.Equal("AdminRole", autorizzazione!.Policy);
    }

    [Fact]
    public void Nessun_controller_e_anonimo_per_intero_senza_dichiararlo()
    {
        // Un [AllowAnonymous] sulla CLASSE apre tutte le sue azioni, comprese quelle aggiunte
        // dopo: e' il modo piu' rapido per allargare il perimetro senza volerlo.
        var interi = Controllers()
            .Where(t => t.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .Select(t => t.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var attesi = SuperficieAttesa
            .Select(x => x.Split('.')[0])
            .Distinct()
            .ToList();

        Assert.All(interi, nome => Assert.Contains(nome, attesi));
    }

    // ─── Ricognizione ────────────────────────────────────────────────────────

    private static IEnumerable<Type> Controllers() =>
        typeof(SmtpSettingsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<(string Nome, bool Anonimo)> Endpoints()
    {
        foreach (var controller in Controllers())
        {
            var classeAnonima = controller.GetCustomAttribute<AllowAnonymousAttribute>() != null;

            var azioni = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.GetCustomAttributes().Any(a => a is HttpMethodAttribute));

            foreach (var azione in azioni)
            {
                var anonima = classeAnonima
                    || azione.GetCustomAttribute<AllowAnonymousAttribute>() != null;

                yield return ($"{controller.Name}.{azione.Name}", anonima);
            }
        }
    }
}
