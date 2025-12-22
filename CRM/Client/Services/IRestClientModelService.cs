using CRM.Client.Models;
using CRM.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IRestClientModelService<T, M, F, K>
    {
        Task<T> Get(K id);

        Task<M> GetDetails(K id);

        Task<PagingResponse<M, S>> Get<S>(F data);


        Task<PagingResponse<M, string>> GetList(F data);
        Task<PagingResponse<M, S>> GetList<S>(F data);

        Task<PagingResponse<M>> GetItems(F data);

        Task<PagingResponse<M>> Get(F data, string folder = null);

        Task<string?> Print(K id);

        Task<APIResponseMessage<T>> Post(T item);

        Task<bool> Delete(K id);
    }
}
