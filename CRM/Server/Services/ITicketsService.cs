using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;

namespace CRM.Server.Services
{
   
    public interface ITicketsService
    {
        // Existing methods
        Task<List<(string?, string?)>> GetEmails(int idTicket);
        Task<List<UserModel>> GetUsersCanAssignTicketAsync(int idTicket);
        Task<List<UserModel>> GetUsersCanAssignTicketTypeAsync(int idType);

        // CRUD
        Task<Ticket?> GetItemAsync(int id);
        Task<TicketDTO?> GetDetailsAsync(int id);
        Task<ObjectView<TicketDTO, string>> GetPagingAsync(TicketFilter args);
        Task<Ticket> PostAsync(Ticket ticket);
        Task<bool> PutAsync(int id, Ticket ticket);
        /// <summary>
        /// Cancella il ticket. Restituisce il motivo del fallimento invece di un <c>bool</c>: una
        /// cancellazione che non riesce e non dice perche' e' indistinguibile da una riuscita male.
        /// </summary>
        Task<DeleteTicketResult> DeleteAsync(int id);

        // Close / ReOpen
        Task<CloseTicketResult> CloseAsync(int id, TicketClose model);

        /// <summary>
        /// Precondizioni di chiusura, cosi' la UI mostra la stessa decisione che <see cref="CloseAsync"/>
        /// applichera' invece di riderivare la regola per conto suo.
        /// </summary>
        Task<TicketClosePreconditionDTO?> GetClosePreconditionAsync(int idTicket);
        Task<bool> ReOpenAsync(int id);
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> BlockAsync(int id, TicketBlockRequest request);
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> UnblockAsync(int id, TicketUnblockRequest request);

        // State
        Task SetTicketStateAsync(TicketDTO ticket);
        Task SetTicketStateAsync(Ticket ticket);

        // Assignment
        Task<List<string>> GetAssignedUserIdsAsync(int idTicket);
        Task<TicketAssignmentContextDTO?> GetAssignmentContextAsync(int idTicket);
        Task<AssignUsersResult> AssignUsersAsync(int idTicket, AssignUsersRequest request, string? currentUserId);
        Task<CRM.Client.Models.APIResponseMessage<TicketDTO>> ClaimAsync(int idTicket, string? currentUserId);

        /// <summary>
        /// Assegna al ticket, in aggiunta a quelle esistenti, gli utenti che non ce l'hanno ancora:
        /// e' la presa in carico implicita di chi registra un intervento su un ticket smistato a
        /// un gruppo. Le assegnazioni sono additive, nessuna viene rimossa.
        /// </summary>
        Task<EnsureAssignedResult> EnsureUsersAssignedAsync(int idTicket, IEnumerable<string> userIds, string? currentUserId);

        /// <summary>
        /// Evento operativo: qualcuno sta registrando lavoro vero sul ticket, e va assegnato al
        /// ticket se non lo era. Non porta piu' a uno stato "in lavorazione" separato: dentro
        /// l'azienda quello stato non esiste, un ticket assegnato e' un ticket su cui si lavora.
        /// </summary>
        Task<bool> StartWorkAsync(int idTicket, IEnumerable<string>? userIds, string? currentUserId);

        /// <summary>
        /// L'elenco dei lavori di chi sta guardando, gia' raggruppato e ordinato: assistenza,
        /// commesse, e in fondo le fasi che non si possono ancora cominciare.
        /// </summary>
        Task<WorkListDTO> GetWorkListAsync();

        // Helpers
        Task<bool> TicketChangeAssigned(int id, string? idAssigned);
        Task CheckTicketExpired(int id);
        Task<int> GetDayBeforeExpired(int id);
    }

    public class AssignUsersResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> AddedUserIds { get; set; } = new();
        public List<string> RemovedUserIds { get; set; } = new();
        public int AssignedCount { get; set; }
        public Ticket? Ticket { get; set; }
    }

    /// <summary>Esito di una chiusura: distingue il rifiuto per precondizioni dall'errore tecnico.</summary>
    public class CloseTicketResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public CloseTicketError Error { get; set; } = CloseTicketError.None;

        public static CloseTicketResult Ok() => new() { Success = true };

        public static CloseTicketResult Fail(CloseTicketError error, string message)
            => new() { Success = false, Error = error, ErrorMessage = message };
    }

    public class DeleteTicketResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DeleteTicketError Error { get; set; } = DeleteTicketError.None;

        public static DeleteTicketResult Ok() => new() { Success = true };

        public static DeleteTicketResult Fail(DeleteTicketError error, string message)
            => new() { Success = false, Error = error, ErrorMessage = message };
    }

    public enum DeleteTicketError
    {
        None = 0,
        TicketNotFound,
        /// <summary>Il database ha rifiutato la cancellazione: il messaggio dice quale vincolo.</summary>
        Unexpected
    }

    public enum CloseTicketError
    {
        None = 0,
        TicketNotFound,
        /// <summary>Il ticket e' bloccato: il blocco va risolto prima.</summary>
        Blocked,
        /// <summary>Il tipo di ticket pretende almeno un intervento e non ce ne sono.</summary>
        InterventionRequired,
        Unexpected
    }

    /// <summary>Esito della presa in carico implicita: chi e' stato assegnato al ticket strada facendo.</summary>
    public class EnsureAssignedResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Codice di errore per far scegliere al chiamante lo status HTTP.</summary>
        public EnsureAssignedError Error { get; set; } = EnsureAssignedError.None;

        /// <summary>Utenti assegnati adesso al ticket perche' non lo erano; vuoto se erano tutti gia' in carico.</summary>
        public List<string> ClaimedUserIds { get; set; } = new();

        /// <summary>Nomi degli utenti in <see cref="ClaimedUserIds"/>, per il messaggio all'operatore.</summary>
        public List<string> ClaimedUserNames { get; set; } = new();

        public Ticket? Ticket { get; set; }
    }

    public enum EnsureAssignedError
    {
        None = 0,
        TicketNotFound,
        Forbidden,
        /// <summary>Un utente selezionato non puo' lavorare su questo ticket (ne' per gruppo ne' per tipo).</summary>
        UserNotEligible,
        Unexpected
    }
}
