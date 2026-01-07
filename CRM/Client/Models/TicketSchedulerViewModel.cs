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
    }
}
