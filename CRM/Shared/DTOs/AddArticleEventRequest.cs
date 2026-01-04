using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public record AddItemEventRequest(
        int DomainId,
        int EventTypeId,
        DateTime? OccurredAt,
        string? Note,
        int? NewOwnerId,
        byte[] ItemDomainStateRowVer  // concorrenza: rowversion del dominio che sto modificando
    );
}
