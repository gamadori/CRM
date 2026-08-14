using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AGUtility.Extensions;

namespace BlazoringComponents.Models
{
    public class SchedulerTicket
    {
        public string Id { get; set; }
        public DateTime DateStart { get; set; }

        public TimeOnly? TimeStart { get; set; }

        public TimeOnly? TimeEnd { get; set; }

        public DateTime DateEnd { get; set; }

        public bool HasExplicitEnd { get; set; }

        public DateTime? DateExpired { get; set; }

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

        public bool IsScheduled { get; set; }

        public DateTime EffectiveDateEnd => HasExplicitEnd ? DateEnd : DateStart;

        /// <summary>
        /// Codice della commessa a cui il ticket appartiene (es. CM-2026-0001), vuoto quando il
        /// ticket non e' lavoro di commessa.
        /// </summary>
        public string CommessaCode { get; set; } = string.Empty;

        public string CommessaFaseName { get; set; } = string.Empty;

        public DateTime? CommessaFaseStartDate { get; set; }

        public DateTime? CommessaFaseEndDate { get; set; }

        /// <summary>
        /// Natura del lavoro: appartenere a una commessa o no. E' un dato di fatto, non una misura
        /// di urgenza — quella resta a priorita' e scadenza, che sono modellate a parte.
        /// </summary>
        public bool IsCommessa => !string.IsNullOrWhiteSpace(CommessaCode);

        public bool HasPhaseWindow => CommessaFaseStartDate.HasValue && CommessaFaseEndDate.HasValue;

        public string PhaseWindowText => HasPhaseWindow
            ? $"{CommessaFaseStartDate:dd/MM} - {CommessaFaseEndDate:dd/MM}"
            : string.Empty;

        public static SchedulerTicket FromItem<TItem>(
            TItem item,
            string dateProperty,
            string timeProperty,
            string dateEndProperty,
            string userProperty,
            string companyProperty,
            string descriptionProperty,
            string backColorProperty)
        {
            var date = item.GetPropertyValueSafe<DateTime?>(dateProperty);
            var dateEnd = item.GetPropertyValueSafe<DateTime?>(dateEndProperty);
            var time = item.GetPropertyValueSafe<TimeOnly?>(timeProperty);

            var model = new SchedulerTicket
            {
                Id = item.GetPropertyValueSafe<object>("Id")?.ToString() ?? string.Empty,
                DateStart = date?.Date ?? DateTime.MinValue,
                TimeStart = time,
                DateEnd = dateEnd ?? DateTime.MinValue,
                HasExplicitEnd = dateEnd.HasValue,
                User = item.GetPropertyValueSafe<string>(userProperty, string.Empty),
                Company = item.GetPropertyValueSafe<string>(companyProperty, string.Empty),
                Description = item.GetPropertyValueSafe<string>(descriptionProperty, string.Empty),
                BackGroundColor = item.GetPropertyValueSafe<string>(backColorProperty, "white"),
                AssignedUserNames = item.GetPropertyValueSafe<List<string>>("AssignedUserNames", new List<string>()),
                StatusColor = item.GetPropertyValueSafe<string>("StatusColor", string.Empty),
                StatusText = item.GetPropertyValueSafe<string>("StatusText", string.Empty),
                CommessaCode = item.GetPropertyValueSafe<string>("CommessaCode", string.Empty),
                CommessaFaseName = item.GetPropertyValueSafe<string>("CommessaFaseName", string.Empty),
                CommessaFaseStartDate = item.GetPropertyValueSafe<DateTime?>("CommessaFaseStartDate"),
                CommessaFaseEndDate = item.GetPropertyValueSafe<DateTime?>("CommessaFaseEndDate"),
                DateExpired = item.GetPropertyValueSafe<DateTime?>("DateExpired")
            };

            model.IsScheduled = (model.TimeStart.HasValue && model.TimeStart.Value != TimeOnly.MinValue)
                || (model.HasExplicitEnd && model.DateEnd > model.DateStart.Date);

            return model;
        }
    }
}
