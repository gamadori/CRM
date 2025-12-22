using CRM.Client.Models;
using CRM.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IAGRestClientService
    {
        Task<T?> GetItem<T, K>(K id, string pathService) where T : class;

        Task<BreadCrumb<T>> GetWithBreadCrumb<T, K>(K id, string root, string pathService);

       


        Task<T?> GetFirst<T>(string pathService) where T : class;

        Task<PagingResponse<T>> Get<T, F>(F data, string pathService) where F : PagingParameterModel;

        Task<PagingResponse<M, S>> Get<M, S, F>(F data, string pathService);

        Task<List<T>> Get<T>(string pathService);

        Task<PagingResponse<T>> GetListPag<F, T>(F data, string pathService) where F : PagingParameterModel, new();

        Task<List<T>> GetList<T, F>(F data, string pathService);

        Task<bool> Delete<K>(K id, string pathService);

        Task<APIResponseMessage<T>> Post<T, K>(T item, string pathService);
        
    }
}
