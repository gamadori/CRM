using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    /// <summary>
    /// Fotografia dell'assegnazione di un ticket, pensata per il picker utenti dell'intervento:
    /// dice chi e' gia' assegnato e a quale gruppo il ticket e' smistato, cosi' la UI puo'
    /// distinguere un utente "in carico" da un candidato che arriva dal gruppo.
    /// <para>
    /// E' una proiezione e non l'entita': serializzare <see cref="Ticket.GroupAssigned"/> porterebbe
    /// dietro Group -> Users -> Groups, un ciclo in JSON.
    /// </para>
    /// </summary>
    public class TicketAssignmentContextDTO
    {
        public int IdTicket { get; set; }

        public int? IdGroupAssigned { get; set; }

        /// <summary>Nome del gruppo assegnato, null se il ticket non e' smistato a nessun gruppo.</summary>
        public string? GroupAssigned { get; set; }

        /// <summary>Utenti con un'assegnazione individuale sul ticket (include il campo legacy IdUserAssigned).</summary>
        public List<string> AssignedUserIds { get; set; } = new();

        public bool Closed { get; set; }

        /// <summary>
        /// True quando il ticket e' smistato a un gruppo ma nessuno l'ha ancora preso in carico:
        /// e' il caso in cui registrare un intervento vale anche come presa in carico.
        /// </summary>
        public bool IsUnclaimedGroupTicket => IdGroupAssigned != null && AssignedUserIds.Count == 0;
    }
}
