using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRM.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRM.Server.Data
{
    /// <summary>
    /// Porta sotto cifratura i segreti scritti prima che la cifratura esistesse, e dice a voce
    /// alta se qualcuno di essi non e' piu' leggibile.
    /// <para>
    /// Senza questo passaggio la cifratura varrebbe solo per il futuro: le righe gia' sul database
    /// resterebbero in chiaro finche' qualcuno non le risalva, e quelle sono proprio le righe che
    /// contengono le credenziali in uso. Il convertitore le legge lo stesso (il marcatore
    /// <c>enc:v1:</c> distingue le due forme), quindi il passaggio si limita a riscriverle.
    /// </para>
    /// <para>
    /// Gira a ogni avvio ed e' idempotente: cerca i valori <b>senza</b> marcatore leggendoli con un
    /// contesto non cifrante, cioe' vedendo cosa c'e' davvero scritto nella colonna. Una seconda
    /// esecuzione non trova piu' niente da fare e non tocca il database.
    /// </para>
    /// </summary>
    public static class SecretsProtectionStartup
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            var protector = services.GetRequiredService<ISecretProtector>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SecretsProtectionStartup));

            if (!protector.Enabled)
                return;

            try
            {
                // Contesto SENZA cifratura: mostra il contenuto vero delle colonne, che e' l'unico
                // modo per distinguere "gia' cifrato" da "ancora in chiaro".
                var options = services.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
                await using var raw = new ApplicationDbContext(options);

                var convertiti = 0;
                var illeggibili = 0;

                foreach (var canale in await raw.SmtpSettings.ToListAsync())
                {
                    convertiti += Converti(protector, canale.Password, v => canale.Password = v, ref illeggibili);
                    convertiti += Converti(protector, canale.ApiKey, v => canale.ApiKey = v, ref illeggibili);
                }

                foreach (var casella in await raw.EmailInboxes.ToListAsync())
                {
                    convertiti += Converti(protector, casella.Password, v => casella.Password = v, ref illeggibili);
                    convertiti += Converti(protector, casella.WebhookToken, v => casella.WebhookToken = v, ref illeggibili);
                }

                if (convertiti > 0)
                {
                    await raw.SaveChangesAsync();
                    logger.LogInformation("Segreti portati sotto cifratura: {Righe} valori.", convertiti);
                }

                if (illeggibili > 0)
                {
                    // Non si blocca l'avvio: il CRM serve anche senza posta, e fermarlo lascerebbe
                    // l'amministratore senza nemmeno la maschera per rimediare. Ma va detto forte,
                    // perche' il sintomo altrimenti sarebbe solo "la posta non parte".
                    logger.LogCritical(
                        "{Righe} segreti non sono decifrabili con le chiavi attuali: la cartella " +
                        "DataProtection:KeysPath non e' quella con cui erano stati cifrati. " +
                        "Vanno reinseriti dalle impostazioni, oppure va ripristinata la cartella giusta.",
                        illeggibili);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Conversione dei segreti non riuscita.");
            }
        }

        /// <summary>
        /// Cifra un valore se e' ancora in chiaro. Restituisce 1 se ha cambiato qualcosa.
        /// Un valore gia' marcato ma illeggibile viene lasciato dov'e': riscriverlo cancellerebbe
        /// l'unica copia rimasta, e con la cartella di chiavi giusta tornerebbe leggibile.
        /// </summary>
        private static int Converti(ISecretProtector protector, string? grezzo, Action<string> assegna, ref int illeggibili)
        {
            if (string.IsNullOrEmpty(grezzo))
                return 0;

            if (DataProtectionSecretProtector.IsProtected(grezzo))
            {
                if (string.IsNullOrEmpty(protector.Unprotect(grezzo)))
                    illeggibili++;

                return 0;
            }

            assegna(protector.Protect(grezzo));
            return 1;
        }
    }
}
