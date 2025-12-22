using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface ISettingsService<T>
    {
        Task<T> Get();

        Task<bool> Post(T model);

    }
}
