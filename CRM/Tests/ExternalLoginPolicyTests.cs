using CRM.Server.Authentication;

namespace CRM.Tests;

/// <summary>
/// Politica di aggancio fra identità esterna e utente CRM. È la regola che decide chi entra,
/// quindi ogni caso è fissato esplicitamente — soprattutto quelli che devono dire di no.
/// </summary>
public class ExternalLoginPolicyTests
{
    // ─── Nessuna creazione di utenti dall'esterno ────────────────────────────

    [Theory]
    [InlineData(ExternalEmailLinking.Disabled)]
    [InlineData(ExternalEmailLinking.WhenProviderVerifies)]
    [InlineData(ExternalEmailLinking.TrustProvider)]
    public void Un_utente_non_censito_non_entra_mai(ExternalEmailLinking linking)
    {
        // Vale anche col provider più fidato: un utente creato al volo non avrebbe azienda,
        // ruolo né gruppi, quindi resterebbe in uno stato indefinito.
        var decisione = ExternalLoginPolicy.Decide(linking, providerVerifiedEmail: true, crmUserExists: false);

        Assert.Equal(ExternalLoginDecision.UnknownUser, decisione);
    }

    // ─── Provider aperti al pubblico: collegamento manuale ───────────────────

    [Fact]
    public void Senza_aggancio_automatico_il_collegamento_resta_manuale()
    {
        var decisione = ExternalLoginPolicy.Decide(
            ExternalEmailLinking.Disabled, providerVerifiedEmail: true, crmUserExists: true);

        Assert.Equal(ExternalLoginDecision.RequireManualLink, decisione);
    }

    [Fact]
    public void Disabled_e_il_default_del_provider()
    {
        // Un provider aggiunto senza specificare la politica non deve agganciare nulla da solo.
        var provider = new ExternalAuthenticationProvider();

        Assert.Equal(ExternalEmailLinking.Disabled, provider.EmailLinking);
    }

    // ─── Aggancio subordinato alla verifica del provider ─────────────────────

    [Fact]
    public void Con_email_verificata_dal_provider_si_aggancia()
    {
        var decisione = ExternalLoginPolicy.Decide(
            ExternalEmailLinking.WhenProviderVerifies, providerVerifiedEmail: true, crmUserExists: true);

        Assert.Equal(ExternalLoginDecision.Link, decisione);
    }

    /// <summary>
    /// Il caso che protegge dall'appropriazione dell'account: senza garanzia sull'email,
    /// chiunque si faccia rilasciare un'identità con quell'indirizzo entrerebbe.
    /// </summary>
    [Fact]
    public void Senza_verifica_dell_email_non_si_aggancia()
    {
        var decisione = ExternalLoginPolicy.Decide(
            ExternalEmailLinking.WhenProviderVerifies, providerVerifiedEmail: false, crmUserExists: true);

        Assert.Equal(ExternalLoginDecision.EmailNotVerified, decisione);
    }

    // ─── Provider aziendale di cui ci si fida ────────────────────────────────

    /// <summary>
    /// Entra ID non emette email_verified per gli account di lavoro: senza questa modalità
    /// l'aggancio automatico su un tenant aziendale non scatterebbe mai.
    /// </summary>
    [Fact]
    public void Con_provider_fidato_si_aggancia_anche_senza_claim_di_verifica()
    {
        var decisione = ExternalLoginPolicy.Decide(
            ExternalEmailLinking.TrustProvider, providerVerifiedEmail: false, crmUserExists: true);

        Assert.Equal(ExternalLoginDecision.Link, decisione);
    }
}
