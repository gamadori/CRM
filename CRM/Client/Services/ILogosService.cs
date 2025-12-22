using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ILogosService
    {
        Task<PagingResponse<Logo>> GetLogos(LogosFilterModel filter);

        Task<bool> PostLogo(Logo logo);

    }
}
