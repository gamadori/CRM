using CRM.Shared;

namespace CRM.Client.Helpers
{
    /// <summary>Mappe di presentazione (icona/colore/testo/badge) per il tipo di attività.
    /// Unica fonte usata da ActivityTimeline e Agenda.</summary>
    public static class ActivityUi
    {
        public static string Icon(ActivityKind k) => k switch
        {
            ActivityKind.Call => "call",
            ActivityKind.Email => "mail",
            ActivityKind.Meeting => "groups",
            ActivityKind.Task => "task_alt",
            _ => "sticky_note_2"
        };

        public static string Color(ActivityKind k) => k switch
        {
            ActivityKind.Call => "#1976d2",
            ActivityKind.Email => "#0288d1",
            ActivityKind.Meeting => "#6a1b9a",
            ActivityKind.Task => "#f9a825",
            _ => "#607d8b"
        };

        public static string KindText(ActivityKind k) => k switch
        {
            ActivityKind.Call => "Chiamata",
            ActivityKind.Email => "Email",
            ActivityKind.Meeting => "Meeting",
            ActivityKind.Task => "Task",
            _ => "Nota"
        };

        public static string BadgeClass(ActivityKind k) => k switch
        {
            ActivityKind.Call => "bg-primary",
            ActivityKind.Email => "bg-info text-dark",
            ActivityKind.Meeting => "bg-purple",
            ActivityKind.Task => "bg-warning text-dark",
            _ => "bg-secondary"
        };
    }
}
