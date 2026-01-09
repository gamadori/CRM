using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazoringComponents.Models
{
    public class SchedulerTicket
    {
        public string Id { get; set; }
        public DateTime DateStart { get; set; }

        public TimeOnly? TimeStart { get; set; }

        public TimeOnly? TimeEnd { get; set; }

        public DateTime DateEnd { get; set; }

        public string BackGroundColor { get; set; }

        public string StatusColor { get; set; }

        public string Company { get; set; }

        /// <summary>
        /// ⚠️ LEGACY: Utente principale (primo assegnato). Mantenuto per compatibilità.
        /// Per visualizzazione multipla, usa AssignedUserNames.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// ✅ NUOVO: Lista di tutti gli utenti assegnati al ticket (nome completo)
        /// </summary>
        public List<string> AssignedUserNames { get; set; } = new List<string>();

        public string Description { get; set; }
    }
}
