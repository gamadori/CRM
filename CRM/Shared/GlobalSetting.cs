using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class GlobalSetting
    {
        public int Id { get; set; }

        [Display(Name = "Giorni Scadenza Ticket")]
        public int TicketDaysExpired { get; set; } = 1;

        [Display(Name = "Schedule Orario Iniziale")]
        public DateTime ScheduleTimeStart { get; set; }

        [Display(Name = "Schedule Orario Finale")]
        public DateTime ScheduleTimeEnd { get; set; }

        [Display(Name = "Scheduler Mensile Massimo Numero Ticket per Giorno")]
        public int MonthlySchedulerMaxNumTickets { get; set; }

        [Display(Name = "Sede Centrale")]
        public int IdHeadQuarter { get; set; }

        [Display(Name = "Telegram")]
        public bool Telegram { get; set; }

        [Display(Name = "Logo per i Reports")]
        public int? LogoReport { get; set; }

        [Display(Name="Logo Header Sito")]
        public int? LogoSiteHeader { get; set; }

        [Display(Name ="Top Row Colore Sfondo")]
        public string? TopRowBgColor { get; set; }
    }
}
