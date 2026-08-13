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
        /// Vale SOLO col rerank spento: con il giudice AI attivo la rosa si forma per classifica
        /// e a scartare è lui, quindi questa soglia viene ignorata (vedi CrmAssistantService).
        /// </summary>
        public double MinSimilarityThreshold { get; set; } = 60.0;

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

        /// <summary>
        /// Id del log di questa risposta: permette all'operatore di lasciare un voto di feedback.
        /// </summary>
        public int? LogId { get; set; }
    }

    /// <summary>
    /// Evento del flusso di risposta dell'assistente unificato (una riga JSON per evento, NDJSON).
    /// Il campo valorizzato dipende da <see cref="Type"/>:
    /// "status" e "delta" e "error" usano <see cref="Text"/>, "tickets" usa <see cref="Tickets"/>,
    /// "logId" usa <see cref="LogId"/>.
    /// </summary>
    public class AssistantStreamEvent
    {
        public const string TypeStatus = "status";
        public const string TypeDelta = "delta";
        public const string TypeTickets = "tickets";
        public const string TypeLogId = "logId";
        public const string TypeError = "error";

        public string Type { get; set; } = TypeDelta;

        public string? Text { get; set; }

        public List<TicketSimilarityResult>? Tickets { get; set; }

        public int? LogId { get; set; }
    }

    /// <summary>Voto di feedback dell'operatore su una risposta dell'assistente.</summary>
    public class AssistantFeedbackRequest
    {
        public int LogId { get; set; }

        /// <summary>1 = pollice su, -1 = pollice giù.</summary>
        public int Vote { get; set; }

        public string? Comment { get; set; }
    }
}
