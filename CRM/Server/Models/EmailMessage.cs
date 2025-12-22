namespace CRM.Server.Models
{
    public class EmailMessage
    {
        public string To { get; init; } = default!;
        public string Subject { get; init; } = default!;
        public string HtmlBody { get; init; } = default!;
        public string? TextBody { get; init; }
    }
}
