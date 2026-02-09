using System.Collections.Generic;

namespace CRM.Shared.DTOs
{
    public class AssignUsersRequest
    {
        public int TicketId { get; set; }
        public List<string> UserIds { get; set; } = new List<string>();
    }
}
