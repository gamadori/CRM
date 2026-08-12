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
    /// Dati minimi senza i quali un'installazione nuova non e' utilizzabile.
    /// <para>
    /// Non e' un "dataset di esempio": qui c'e' solo cio' che il codice da' per esistente. Le lingue,
    /// perche' senza nemmeno una riga <c>LanguagesService.GetIdLanguage()</c> torna null e i nomi
    /// tradotti spariscono ovunque in silenzio; gli stati ticket, perche' sono una tabella che
    /// rispecchia <see cref="eTicketStates"/> e senza le sue righe ogni passaggio di stato non
    /// trova dove andare; una riga di impostazioni generali e un tipo ticket, perche' altrimenti
    /// non si riesce nemmeno ad aprire il primo ticket.
    /// </para>
    /// <para>
    /// <b>Additivo e idempotente.</b> Gira a ogni avvio, anche sul database di produzione: inserisce
    /// solo cio' che manca, confrontando per chiave naturale (codice lingua, valore dello stato) e
    /// non per Id. Non aggiorna e non cancella mai niente - un'installazione che ha rinominato i
    /// propri stati o cambiato i colori se li tiene. Se una tabella ha gia' delle righe, il seeder
    /// la lascia stare: meglio incompleta a modo suo che riscritta a modo nostro.
    /// </para>
    /// </summary>
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

            try
            {
                var inserite = 0;

                inserite += await SeedLanguagesAsync(context);
                inserite += await SeedTicketStatesAsync(context);
                inserite += await SeedGlobalSettingsAsync(context);
                inserite += await SeedTicketTypesAsync(context);

                if (inserite > 0)
                    logger.LogInformation("Dati iniziali: inserite {Righe} righe mancanti.", inserite);
            }
            catch (Exception ex)
            {
                // Un seed che non riesce non deve impedire l'avvio: il server parte comunque e il
                // problema si vede nel log. Bloccare qui vorrebbe dire un'applicazione che non si
                // apre per una tabella di appoggio.
                logger.LogError(ex, "Dati iniziali non inseriti.");
            }
        }

        /// <summary>
        /// Le cinque lingue dell'applicazione. Il codice e' quello completo di cultura perche'
        /// <c>LanguagesService</c> cerca "it-IT" per la lingua di ripiego: con il codice corto
        /// quel ripiego non troverebbe niente e cadrebbe sulla prima riga per Id.
        /// </summary>
        private static async Task<int> SeedLanguagesAsync(ApplicationDbContext context)
        {
            var attese = new[]
            {
                new Language { Name = "Italiano", Description = "Italiano", LanguageCode = "it-IT", Index = 0 },
                new Language { Name = "English", Description = "English", LanguageCode = "en-US", Index = 1 },
                new Language { Name = "Français", Description = "Français", LanguageCode = "fr-FR", Index = 2 },
                new Language { Name = "Deutsch", Description = "Deutsch", LanguageCode = "de-DE", Index = 3 },
                new Language { Name = "Español", Description = "Español", LanguageCode = "es-ES", Index = 4 }
            };

            var presenti = await context.Languages
                .Select(x => x.LanguageCode)
                .ToListAsync();

            var mancanti = attese
                .Where(l => !presenti.Contains(l.LanguageCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (mancanti.Count == 0)
                return 0;

            context.Languages.AddRange(mancanti);
            await context.SaveChangesAsync();

            return mancanti.Count;
        }

        /// <summary>
        /// Una riga per ogni valore di <see cref="eTicketStates"/>. La colonna <c>State</c> e' il
        /// valore dell'enum ed e' quella su cui il codice cerca: descrizione e colore invece sono
        /// solo etichette, e chi le cambia se le tiene.
        /// </summary>
        private static async Task<int> SeedTicketStatesAsync(ApplicationDbContext context)
        {
            var attesi = new (eTicketStates Stato, string Descrizione, string Colore)[]
            {
                (eTicketStates.Created,    "Aperto",        "#0d6efd"),
                (eTicketStates.Assigned,   "Assegnato",     "#6f42c1"),
                (eTicketStates.Processing, "In lavorazione","#fd7e14"),
                (eTicketStates.Expired,    "Scaduto",       "#dc3545"),
                (eTicketStates.Closed,     "Chiuso",        "#198754")
            };

            var presenti = await context.TicketStates
                .Select(x => x.State)
                .ToListAsync();

            var mancanti = attesi
                .Where(s => !presenti.Contains((int)s.Stato))
                .Select(s => new TicketState
                {
                    State = (int)s.Stato,
                    Description = s.Descrizione,
                    Color = s.Colore
                })
                .ToList();

            if (mancanti.Count == 0)
                return 0;

            context.TicketStates.AddRange(mancanti);
            await context.SaveChangesAsync();

            return mancanti.Count;
        }

        /// <summary>
        /// La riga delle impostazioni generali. Ne esiste una sola: se c'e' gia', non si tocca -
        /// i valori li ha scelti l'amministratore, e i default del modello valgono solo alla nascita.
        /// </summary>
        private static async Task<int> SeedGlobalSettingsAsync(ApplicationDbContext context)
        {
            if (await context.GlobalSettings.AnyAsync())
                return 0;

            context.GlobalSettings.Add(new GlobalSetting());
            await context.SaveChangesAsync();

            return 1;
        }

        /// <summary>
        /// Un tipo ticket per poterne aprire uno. E' l'unica riga di questo seeder che non discende
        /// dal codice ma da una scelta: quale sia il lavoro dell'azienda non lo sappiamo, quindi si
        /// mette un tipo generico da rinominare. Si inserisce solo su tabella vuota - dove esiste
        /// gia' una configurazione, aggiungere "Assistenza" sarebbe rumore.
        /// </summary>
        private static async Task<int> SeedTicketTypesAsync(ApplicationDbContext context)
        {
            if (await context.TicketTypes.AnyAsync())
                return 0;

            context.TicketTypes.Add(new TicketType
            {
                Desc = "Assistenza",
                CustomerEnabled = true,
                RequiresIntervention = true
            });

            await context.SaveChangesAsync();

            return 1;
        }
    }
}
