using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IReportService<T, F>
    {
        Task<T> Get(F filter);

        Task<List<T>> GetItems();
    }
}
