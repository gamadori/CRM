using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public record EventTypeDto(int EventTypeId, int DomainId, string Code, string Name, bool RequiresOwner);
}
