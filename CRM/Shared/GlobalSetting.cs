using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    /// <summary>
    /// Modalità di input vocale dell'assistente AI, scelta dall'amministratore.
    /// Off: nessun microfono. Browser: dettatura lato client con la Web Speech API
    /// (nessun costo, richiede Chrome/Edge). Whisper: registrazione inviata al server
    /// e trascritta con OpenAI Whisper (più accurata, multilingua, ha un costo).
    /// </summary>
    public enum VoiceInputMode
    {
        [Display(Name = "Disattivata")]
        Off = 0,

        [Display(Name = "Browser (dettatura locale, gratis, solo Chrome/Edge)")]
        Browser = 1,

        [Display(Name = "Server (Whisper OpenAI, più accurata, a consumo)")]
        Whisper = 2
    }

    /// <summary>
    /// Come l'assistente AI verifica la pertinenza dei ticket simili prima di presentarli
    /// come "Ticket di riferimento". Off: solo soglia di similarità + istruzioni di prompt.
    /// AgentReread: l'assistente rilegge il ticket completo (get_ticket_details) prima di citarlo.
    /// LlmJudge: un giudice AI dedicato (modello veloce) rilegge i candidati e scarta i non pertinenti.
    /// </summary>
    public enum TicketRerankMode
    {
        [Display(Name = "Nessuno (solo soglia e istruzioni)")]
        Off = 0,

        [Display(Name = "Rilettura da parte dell'assistente")]
        AgentReread = 1,

        [Display(Name = "Giudice AI dedicato (rilegge e filtra)")]
        LlmJudge = 2
    }

    public class GlobalSetting
    {
        public int Id { get; set; }

        [Display(Name = "Giorni Scadenza Ticket")]
        public int TicketDaysExpired { get; set; } = 1;

        [Display(Name = "Schedule Orario Iniziale")]
        public TimeOnly? ScheduleTimeStart { get; set; }

        [Display(Name = "Schedule Orario Finale")]
        public TimeOnly? ScheduleTimeEnd { get; set; }

        [Display(Name = "Scheduler Mensile Massimo Numero Ticket per Giorno")]
        public int MonthlySchedulerMaxNumTickets { get; set; }

        [Display(Name = "Telegram")]
        public bool Telegram { get; set; }

        [Display(Name = "Richiedi firma da remoto (interventi Telefono/Web/Remoto)")]
        public bool RemoteSignatureEnabled { get; set; }

        [Display(Name = "Logo per i Reports")]
        public int? LogoReport { get; set; }

        [Display(Name="Logo Header Sito")]
        public int? LogoSiteHeader { get; set; }

        [Display(Name ="Top Row Colore Sfondo")]
        public string? TopRowBgColor { get; set; }

        [Display(Name = "Aliquota IVA predefinita (%)")]
        public decimal DefaultVatRate { get; set; } = 22;

        [Display(Name = "Regime fiscale (FatturaPA, es. RF01)")]
        public string? RegimeFiscale { get; set; } = "RF01";

        /// <summary>
        /// Valuta di casa: e' quella in cui vengono espressi i totali delle note spese, e verso
        /// cui si convertono le spese sostenute all'estero.
        /// </summary>
        [Display(Name = "Valuta base (ISO, es. EUR)")]
        [MaxLength(3)]
        public string BaseCurrency { get; set; } = "EUR";

        [Display(Name = "Input vocale assistente AI")]
        public VoiceInputMode VoiceInputMode { get; set; } = VoiceInputMode.Off;

        [Display(Name = "Verifica pertinenza ticket di riferimento (assistente AI)")]
        public TicketRerankMode TicketRerankMode { get; set; } = TicketRerankMode.Off;

        [Display(Name = "Preavviso ticket attivo")]
        public bool TicketReminderEnabled { get; set; } = true;

        [Display(Name = "Preavviso appuntamento ticket (minuti prima)")]
        public int TicketAppointmentReminderMinutes { get; set; } = 60;

        [Display(Name = "Preavviso scadenza ticket (minuti prima)")]
        public int TicketExpiryReminderMinutes { get; set; } = 120;

        // ─── Default preavviso attività per tipo (minuti prima della scadenza) ───
        // Precompilano il Promemoria alla creazione dell'attività; null = nessun default.
        [Display(Name = "Preavviso Chiamata (minuti prima)")]
        public int? ActivityReminderMinutesCall { get; set; }

        [Display(Name = "Preavviso Email (minuti prima)")]
        public int? ActivityReminderMinutesEmail { get; set; }

        [Display(Name = "Preavviso Meeting (minuti prima)")]
        public int? ActivityReminderMinutesMeeting { get; set; }

        [Display(Name = "Preavviso Task (minuti prima)")]
        public int? ActivityReminderMinutesTask { get; set; }

        [Display(Name = "Preavviso Nota (minuti prima)")]
        public int? ActivityReminderMinutesNote { get; set; }

        /// <summary>Anticipo di default (minuti) del promemoria per il tipo di attività indicato, o null se non impostato.</summary>
        public int? ActivityReminderMinutes(ActivityKind kind) => kind switch
        {
            ActivityKind.Call => ActivityReminderMinutesCall,
            ActivityKind.Email => ActivityReminderMinutesEmail,
            ActivityKind.Meeting => ActivityReminderMinutesMeeting,
            ActivityKind.Task => ActivityReminderMinutesTask,
            ActivityKind.Note => ActivityReminderMinutesNote,
            _ => null
        };
    }
}
