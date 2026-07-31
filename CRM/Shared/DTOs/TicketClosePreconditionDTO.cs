namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Precondizioni di chiusura di un ticket, calcolate dal server.
    /// <para>
    /// Esiste perche' la UI non deve riderivare la regola: mostra la stessa decisione che
    /// <c>CloseAsync</c> applichera'. Se il client calcolasse per conto suo, prima o poi le due
    /// versioni divergerebbero e l'operatore vedrebbe un pulsante attivo su una chiusura che il
    /// server rifiuta.
    /// </para>
    /// </summary>
    public class TicketClosePreconditionDTO
    {
        public int IdTicket { get; set; }

        /// <summary>True se il tipo di ticket pretende almeno un intervento registrato.</summary>
        public bool RequiresIntervention { get; set; }

        /// <summary>Interventi registrati sul ticket.</summary>
        public int InterventionCount { get; set; }

        /// <summary>Ticket gia' chiuso: la pagina di chiusura e' in sola lettura.</summary>
        public bool Closed { get; set; }

        public bool IsBlocked { get; set; }

        /// <summary>False se la chiusura verrebbe rifiutata; <see cref="BlockReason"/> spiega perche'.</summary>
        public bool CanClose { get; set; }

        /// <summary>Motivo del rifiuto, null quando <see cref="CanClose"/> e' true.</summary>
        public string? BlockReason { get; set; }

        /// <summary>
        /// La modalita' di intervento si chiede in chiusura solo quando nessun intervento la porta:
        /// sui tipi che pretendono l'intervento il dato e' sull'intervento, non sul ticket.
        /// </summary>
        public bool ShowSupportField => !RequiresIntervention;
    }

    /// <summary>
    /// Esito di una chiusura visto dal client. Porta il messaggio del server: un rifiuto per
    /// intervento mancante o ticket bloccato e' un'informazione azionabile, non un fallimento muto.
    /// </summary>
    public class CloseTicketResponse
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

        public static CloseTicketResponse Ok() => new() { Success = true };

        public static CloseTicketResponse Fail(string message)
            => new() { Success = false, ErrorMessage = message };
    }
}
