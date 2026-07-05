using CRM.Client.Models;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Client.Services
{

    public interface IUserService : IAGRestClientService 
    {
        Task<ApplicationUser> Confirm(string id);

        Task<bool> SendInvite(string id);

        Task<ApplicationUser> CurrentUser();

        Task<bool> CheckPolicy(ePolicy policy);

        Task<CompanyTypes> GetCompanyType();

        Task<bool> Disable(string id);

       
    }

    public interface IIdentityRestService
    {
        Task<Company> Get(int id);


        Task<PagingResponse<Company>> Get(CompanyFilter filter, string folder = null);

        Task<APIResponseMessage<Company>> Post(Company item);

        Task<bool> Delete(int id);

        Task<bool> Confirm(string id);

    }

    public interface IBaseRestService<T, F, K> where F : class 
    {
        Task<T> Get(K id);

        Task<List<T>> Get();

        Task<PagingResponse<T>> Get(F filter, string folder = null);

        Task<PagingResponse<T>> GetList(F filter);

        Task<APIResponseMessage<T>> Post(T item);

        Task<bool> Delete(K id);

        Task<List<I>> GetItems<I>(F data);
    }

    public interface IRestService<T, F> where T : class where F : class, new()
    {
        Task<T> Get(int id);

       

        Task<PagingResponse<T>> Get(F filter = null);

        Task<APIResponseMessage<T>> Post(T item);

        Task<bool> Delete(int id);
    }

   

    public interface IRestService<T>
    {
        Task<T> Get();

    }

}
