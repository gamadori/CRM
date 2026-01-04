using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared.DTOs
{
    public record DomainStateDto(
        int Id,
        int DomainId,
        string DomainCode, 
        int StateId,
        string StateCode,
        byte[] RowVersion
    );
}
