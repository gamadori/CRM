using System;

namespace CRM.Shared.DTOs
{
    public class TicketSummaryProposalRequest
    {
        public int MaxMessages { get; set; } = 40;
    }

    public class TicketSummaryProposalResponse
    {
        public int IdTicket { get; set; }
        public string Summary { get; set; } = string.Empty;
        public bool GeneratedByAi { get; set; }
        public string? Warning { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int SourceMessageCount { get; set; }
        public DateTime? LastMessageAt { get; set; }
    }

    public class UpdateTicketSummaryRequest
    {
        public string Summary { get; set; } = string.Empty;
    }
}
