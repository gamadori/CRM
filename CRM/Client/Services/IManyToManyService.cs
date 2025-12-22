using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IManyToManyService<T>
    {
        Task<bool> Post(T item);

        Task<bool> Delete(T item);
    }
}
