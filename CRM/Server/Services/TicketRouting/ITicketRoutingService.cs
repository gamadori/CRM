using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services.TicketRouting
{
    /// <summary>Da dove arriva il ticket appena creato: serve solo a rispettare le impostazioni.</summary>
    public enum TicketRoutingSource
    {
        /// <summary>Ticket aperto dall'applicazione o dalle API (cliente, operatore, integrazione).</summary>
        Application = 0,

        /// <summary>Ticket aperto automaticamente da un'email in arrivo.</summary>
        InboundEmail = 1
    }

    /// <summary>Esito dello smistamento, per chi ha bisogno di sapere com'e' andata (log, notifiche, test).</summary>
    /// <param name="Executed">False se lo smistamento non e' stato nemmeno tentato (disattivato, ticket gia' assegnato).</param>
    /// <param name="AssignedGroupId">Gruppo effettivamente assegnato al ticket, se lo smistamento lo ha deciso.</param>
    /// <param name="SuggestedGroupId">Gruppo proposto dall'AI, anche quando resta un semplice suggerimento.</param>
    /// <param name="Confidence">Confidenza del suggerimento.</param>
    /// <param name="Outcome">Stato registrato sul ticket.</param>
    public record TicketRoutingResult(
        bool Executed,
        int? AssignedGroupId,
        int? SuggestedGroupId,
        double? Confidence,
        AiRoutingOutcome Outcome)
    {
        public static readonly TicketRoutingResult NotExecuted =
            new(false, null, null, null, AiRoutingOutcome.None);
    }

    /// <summary>
    /// Smistamento automatico dei ticket verso i gruppi di lavoro: sceglie il gruppo con l'AI,
    /// lo assegna se la confidenza supera la soglia, altrimenti lascia il ticket in coda con il
    /// suggerimento in evidenza. Non solleva mai: un problema qui non deve impedire l'apertura
    /// di un ticket.
    /// </summary>
    public interface ITicketRoutingService
    {
        /// <summary>Smista il ticket appena creato. Non fa nulla se ha gia' un gruppo assegnato.</summary>
        Task<TicketRoutingResult> RouteAsync(int idTicket, TicketRoutingSource source, CancellationToken ct = default);

        /// <summary>Prova lo smistamento su un testo qualsiasi, senza creare ne' modificare nulla.</summary>
        Task<TicketRoutingPreviewResult> PreviewAsync(TicketRoutingPreviewRequest request, CancellationToken ct = default);

        /// <summary>Assegna al ticket il gruppo suggerito dall'AI (decisione dell'operatore).</summary>
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> AcceptSuggestionAsync(int idTicket, CancellationToken ct = default);

        /// <summary>Scarta il suggerimento: il ticket resta in coda, senza piu' proposte in evidenza.</summary>
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> DismissSuggestionAsync(int idTicket, CancellationToken ct = default);

        /// <summary>Configurazione corrente (riga singola, creata al primo accesso).</summary>
        Task<TicketRoutingSetting> GetSettingsAsync(CancellationToken ct = default);

        /// <summary>Stato di salute dello smistamento per la pagina di configurazione.</summary>
        Task<TicketRoutingStatusDTO> GetStatusAsync(CancellationToken ct = default);
    }
}
