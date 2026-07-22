using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>
    /// Servizio client dell'assistente AI unificato (dati CRM + soluzioni dai ticket chiusi).
    /// </summary>
    public interface IAssistantService
    {
        /// <summary>
        /// Invia la conversazione e riceve gli eventi del flusso di risposta man mano che
        /// arrivano dal server (NDJSON): testo, stato dei tool, ticket di riferimento, id log.
        /// Gli errori vengono consegnati come evento di tipo "error".
        /// </summary>
        Task ChatStream(AssistantChatRequest request, Action<AssistantStreamEvent> onEvent);

        /// <summary>Invia il voto di feedback dell'operatore su una risposta.</summary>
        Task<bool> SendFeedback(AssistantFeedbackRequest request);

        /// <summary>
        /// Invia l'audio registrato dal microfono e ne riceve la trascrizione testuale
        /// (modalità vocale "Server"/Whisper). L'esito include l'eventuale messaggio d'errore.
        /// </summary>
        Task<VoiceTranscriptionResult> Transcribe(byte[] audio, string contentType, string fileName);

        /// <summary>Elenco dei log Q&amp;A dell'assistente (consultazione admin).</summary>
        Task<List<AssistantChatLogDTO>> GetLogs(AssistantChatLogFilter filter);
    }

    /// <summary>Esito della trascrizione vocale: testo riconosciuto oppure motivo dell'errore.</summary>
    public class VoiceTranscriptionResult
    {
        public bool Success { get; set; }

        public string? Text { get; set; }

        public string? Error { get; set; }
    }
}
