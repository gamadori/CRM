using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public record AvailableEventsResponse(
        int DomainId,
        string DomainCode,
        int CurrentStateId,
        string CurrentStateCode,
        IReadOnlyList<EventTypeDto> Available
    );
}
