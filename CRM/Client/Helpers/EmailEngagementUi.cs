using CRM.Shared;

namespace CRM.Client.Helpers
{
    /// <summary>
    /// Mappatura condivisa stato/evento di engagement email → aspetto (classe badge, etichetta, icona
    /// Material). Usata da EmailsSent (Index/Details) e dalla timeline attività per un look coerente.
    /// </summary>
    public static class EmailEngagementUi
    {
        public static (string Css, string Label, string Icon) Status(EmailEngagementStatus status) => status switch
        {
            EmailEngagementStatus.Delivered => ("bg-info", "Consegnata", "mark_email_read"),
            EmailEngagementStatus.Opened => ("bg-primary", "Aperta", "drafts"),
            EmailEngagementStatus.Clicked => ("bg-success", "Cliccata", "ads_click"),
            EmailEngagementStatus.Bounced => ("bg-danger", "Rimbalzata", "error"),
            EmailEngagementStatus.SpamReported => ("bg-warning text-dark", "Spam", "report"),
            EmailEngagementStatus.Unsubscribed => ("bg-dark", "Disiscritto", "unsubscribe"),
            _ => ("bg-secondary", "Inviata", "schedule")
        };

        public static (string Css, string Label, string Icon) Event(EmailEventType type) => type switch
        {
            EmailEventType.Delivered => ("bg-info", "Consegnata", "mark_email_read"),
            EmailEventType.Opened => ("bg-primary", "Aperta", "drafts"),
            EmailEventType.Clicked => ("bg-success", "Cliccata", "ads_click"),
            EmailEventType.Bounced => ("bg-danger", "Rimbalzata", "error"),
            EmailEventType.SpamReported => ("bg-warning text-dark", "Spam", "report"),
            EmailEventType.Unsubscribed => ("bg-dark", "Disiscritto", "unsubscribe"),
            EmailEventType.Deferred => ("bg-secondary", "Rimandata", "schedule"),
            EmailEventType.Dropped => ("bg-danger", "Scartata", "block"),
            _ => ("bg-secondary", "Evento", "info")
        };
    }
}
