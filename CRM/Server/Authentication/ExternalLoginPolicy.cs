namespace CRM.Server.Authentication
{
    /// <summary>Cosa fare di un'identità esterna che non risulta ancora collegata a un utente CRM.</summary>
    internal enum ExternalLoginDecision
    {
        /// <summary>Identità agganciata all'utente trovato per email, poi accesso.</summary>
        Link,

        /// <summary>L'utente esiste ma il collegamento va fatto a mano, da autenticato.</summary>
        RequireManualLink,

        /// <summary>Nessun utente CRM con quell'email: non si crea niente.</summary>
        UnknownUser,

        /// <summary>Il provider non garantisce l'email, quindi non ci si aggancia.</summary>
        EmailNotVerified
    }

    /// <summary>
    /// Decisione pura sull'aggancio di un'identità esterna, isolata dal resto per poterla
    /// verificare: è la regola che determina chi entra nel CRM e come.
    ///
    /// Il principio è che il CRM non crea utenti da un login esterno. Un <c>ApplicationUser</c>
    /// ha azienda, ruolo e gruppi che pilotano permessi e fasi di commessa, e un'identità
    /// Microsoft o Google non porta nulla di tutto questo: un utente creato al volo resterebbe
    /// in uno stato indefinito. L'utente deve esistere già.
    /// </summary>
    internal static class ExternalLoginPolicy
    {
        public static ExternalLoginDecision Decide(
            ExternalEmailLinking linking,
            bool providerVerifiedEmail,
            bool crmUserExists)
        {
            // Nessun auto-provisioning, qualunque sia la politica di aggancio.
            if (!crmUserExists)
                return ExternalLoginDecision.UnknownUser;

            return linking switch
            {
                ExternalEmailLinking.TrustProvider => ExternalLoginDecision.Link,

                ExternalEmailLinking.WhenProviderVerifies => providerVerifiedEmail
                    ? ExternalLoginDecision.Link
                    : ExternalLoginDecision.EmailNotVerified,

                _ => ExternalLoginDecision.RequireManualLink
            };
        }
    }
}
