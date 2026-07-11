using System.Collections.Generic;

namespace CRM.Shared.Models
{
    /// <summary>
    /// Un singolo messaggio nella conversazione con l'assistente AI.
    /// </summary>
    public class AssistantChatMessage
    {
        /// <summary>
        /// Ruolo del messaggio: "user" oppure "assistant".
        /// </summary>
        public string Role { get; set; } = "user";

        /// <summary>
        /// Contenuto testuale del messaggio.
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Richiesta di conversazione all'assistente AI sui ticket chiusi.
    /// </summary>
    public class AssistantChatRequest
    {
        /// <summary>
        /// Storico della conversazione. L'ultimo messaggio deve essere dell'utente.
        /// </summary>
        public List<AssistantChatMessage> Messages { get; set; } = new();

        /// <summary>
        /// Numero massimo di ticket simili da recuperare come contesto.
        /// </summary>
        public int TopTickets { get; set; } = 5;

        /// <summary>
        /// Soglia minima di similarità (0-100) per considerare un ticket rilevante.
        /// </summary>
        public double MinSimilarityThreshold { get; set; } = 55.0;

        /// <summary>
        /// Ticket di contesto (opzionale): se la chat parte da un ticket, il suo modello
        /// viene usato per dare priorità alla conoscenza specifica di quel prodotto.
        /// </summary>
        public int? IdTicket { get; set; }

        /// <summary>
        /// Modello/prodotto di contesto (opzionale): forza la rilevanza della conoscenza
        /// associata a questo prodotto, anche in assenza di ticket chiusi simili.
        /// </summary>
        public int? IdProduct { get; set; }
    }

    /// <summary>
    /// Risposta dell'assistente AI.
    /// </summary>
    public class AssistantChatResponse
    {
        /// <summary>
        /// Testo della risposta generata dall'assistente.
        /// </summary>
        public string Reply { get; set; } = string.Empty;

        /// <summary>
        /// Ticket chiusi usati come fonte per la risposta.
        /// </summary>
        public List<TicketSimilarityResult> ReferencedTickets { get; set; } = new();

        /// <summary>
        /// True se la richiesta è stata elaborata con successo.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Messaggio informativo o di errore (mostrato in caso di problemi).
        /// </summary>
        public string? Message { get; set; }
    }
}
