using System;

namespace CRM.Shared
{
    /// <summary>Stato di engagement di una email (progressione: Inviata → Consegnata → Aperta → Cliccata; oppure esiti negativi).</summary>
    public enum EmailEngagementStatus
    {
        Sent = 0,
        Delivered = 1,
        Opened = 2,
        Clicked = 3,
        Bounced = 4,
        SpamReported = 5,
        Unsubscribed = 6
    }

    public class EmailSent
    {
        public int Id { get; set; }

        public DateTime DateSent { get; set; }

        public string IdUser { get; set; }

        public string To { get; set; }

        public string CC { get; set; }

        public string Attchments { get; set; }

        public string Subject { get; set; }

        public string Message { get; set; }

        public string Result { get; set; }

        public ReportTypes ReportType { get; set; }

        // ---- Engagement (Tier 3): alimentato dai webhook dei provider ESP ----

        /// <summary>Identificatore di correlazione inviato al provider (custom arg/tag) e restituito negli eventi webhook.</summary>
        public string? MessageRef { get; set; }

        public EmailEngagementStatus EngagementStatus { get; set; } = EmailEngagementStatus.Sent;

        public DateTime? DeliveredAt { get; set; }

        public DateTime? OpenedAt { get; set; }

        public int OpenCount { get; set; }

        public DateTime? LastClickedAt { get; set; }

        public int ClickCount { get; set; }

        public DateTime? BouncedAt { get; set; }

        public string? BounceReason { get; set; }

        /// <summary>Istante dell'ultimo evento di engagement ricevuto.</summary>
        public DateTime? LastEventAt { get; set; }
    }

    public class EmailSentFilterModel : PagingParameterModel
    {
    }
}
