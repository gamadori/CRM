using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Models
{
    public class TicketSchedulerViewModel
    {
        public int Id { get; set; }

        public DateTime? Date { get; set; }

        public TimeOnly? Time { get; set; }

        public DateTime? DateEnd { get; set; }   
        
        /// <summary>
        /// ⚠️ LEGACY: Utente principale. Usa AssignedUserNames per lista completa.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// ✅ NUOVO: Lista di tutti gli utenti assegnati (ID)
        /// </summary>
        public List<string> AssignedUserIds { get; set; } = new List<string>();

        /// <summary>
        /// ✅ NUOVO: Lista di tutti gli utenti assegnati (nomi completi)
        /// </summary>
        public List<string> AssignedUserNames { get; set; } = new List<string>();

        public string BackColor { get; set; }
        
        public string Company { get; set; }

        public string Description { get; set; }

        public int Status { get; set; }

        public bool Expired { get; set; }

        /// <summary>
        /// ✅ NUOVO: Colore dello stato del ticket (es. #28a745 per "Aperto")
        /// </summary>
        public string StatusColor { get; set; }

        /// <summary>
        /// ✅ NUOVO: Descrizione testuale dello stato (es. "Aperto", "In Lavorazione", "Chiuso")
        /// </summary>
        public string StatusText { get; set; }

        public int? IdCommessa { get; set; }

        /// <summary>
        /// Codice commessa (es. CM-2026-0001), vuoto sui ticket di assistenza. Lo scheduler legge
        /// questa proprieta' per nome via reflection: rinominandola va aggiornato anche
        /// SchedulerTicket nei quattro componenti di vista.
        /// </summary>
        public string CommessaCode { get; set; } = string.Empty;
    }
}
