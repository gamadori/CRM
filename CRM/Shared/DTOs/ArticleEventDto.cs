using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public record ArticleEventDto (
        long ItemEventId,
        long ItemId,
        int DomainId,
        string DomainCode,
        int EventTypeId,
        string EventTypeCode,
        string EventTypeName,
        int? FromStateId,
        string? FromStateCode,
        int? ToStateId,
        string? ToStateCode,
        DateTime OccurredAt,
        string? Note,
        int? NewOwnerId
    );
}
