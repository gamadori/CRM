using CRM.Shared;
using CRM.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IDealService : IDataService<Deal, DealDTO, int, DealFilter, decimal>
    {
        Task<CommercialForecastDTO?> GetForecastAsync(DealForecastFilter? filter);
    }
}
