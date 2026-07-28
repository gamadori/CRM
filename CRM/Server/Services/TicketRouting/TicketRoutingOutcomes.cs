using CRM.Shared;

namespace CRM.Server.Services.TicketRouting
{
    /// <summary>
    /// Come cambia l'esito di un suggerimento quando il gruppo del ticket viene toccato a mano.
    /// E' la misura su cui si tara lo smistamento: senza distinguere "accettato" da "corretto"
    /// non si sa se l'AI stia aiutando o creando lavoro.
    /// </summary>
    public static class TicketRoutingOutcomes
    {
        /// <summary>Esito da registrare dopo un cambio di gruppo deciso da una persona.</summary>
        /// <param name="current">Esito attualmente sul ticket.</param>
        /// <param name="suggestedGroupId">Gruppo proposto a suo tempo dall'AI.</param>
        /// <param name="newGroupId">Gruppo con cui il ticket resta dopo la modifica.</param>
        public static AiRoutingOutcome AfterGroupChange(AiRoutingOutcome current, int? suggestedGroupId, int? newGroupId)
        {
            // Nessun suggerimento: non c'e' niente da valutare.
            if (suggestedGroupId == null)
                return current;

            // Gruppo rimosso: se era stato messo dall'AI, e' una correzione a tutti gli effetti.
            if (newGroupId == null)
                return current == AiRoutingOutcome.Accepted ? AiRoutingOutcome.Corrected : current;

            return newGroupId == suggestedGroupId
                ? AiRoutingOutcome.Accepted
                : AiRoutingOutcome.Corrected;
        }
    }
}
