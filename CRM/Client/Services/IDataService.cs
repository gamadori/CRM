using CRM.Client.Models;
using CRM.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IDataService<T, K, F, S> 
    {
        Task<T?> GetItemAsync(K id);

        Task<T?> GetFirstAsync();

        Task<PagingResponse<T, S>?> GetSummaryAsync(F? data);

        Task<PagingResponse<T>?> GetPagingAsync(F? data);

        Task<List<T>?> GetListAsync(F? data);

        Task<APIResponseMessage<T>> PostAsync(T item);

        Task<bool> DeleteAsync(K id);

    }

    public interface IDataService<T, M, K, F, S>
    {
        Task<M?> GetItemAsync(K id);

        Task<M?> GetFirstAsync();

        Task<PagingResponse<M, S>?> GetSummaryAsync(F? data);

        Task<PagingResponse<M>?> GetPagingAsync(F? data);

        Task<List<M>?> GetListAsync(F? data);

        Task<APIResponseMessage<M>> PostAsync(T item);

        Task<bool> DeleteAsync(K id);

    }

    public interface IDataService<T>
    {

        Task<T?> GetFirstAsync();


        Task<APIResponseMessage<T>> PostAsync(T item);


    }

}
