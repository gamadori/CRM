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

        /// <summary>Elenco dei log Q&amp;A dell'assistente (consultazione admin).</summary>
        Task<List<AssistantChatLogDTO>> GetLogs(AssistantChatLogFilter filter);
    }
}
