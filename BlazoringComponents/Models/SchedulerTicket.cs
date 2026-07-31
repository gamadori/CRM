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

        /// <summary>
        /// ✅ Colore dello stato del ticket (es. #28a745 per "Aperto")
        /// </summary>
        public string StatusColor { get; set; }

        /// <summary>
        /// ✅ NUOVO: Descrizione testuale dello stato (es. "Aperto", "In Lavorazione", "Chiuso")
        /// </summary>
        public string StatusText { get; set; }

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

        /// <summary>
        /// Codice della commessa a cui il ticket appartiene (es. CM-2026-0001), vuoto quando il
        /// ticket non e' lavoro di commessa.
        /// </summary>
        public string CommessaCode { get; set; } = string.Empty;

        /// <summary>
        /// Natura del lavoro: appartenere a una commessa o no. E' un dato di fatto, non una misura
        /// di urgenza — quella resta a priorita' e scadenza, che sono modellate a parte.
        /// </summary>
        public bool IsCommessa => !string.IsNullOrWhiteSpace(CommessaCode);
    }
}
