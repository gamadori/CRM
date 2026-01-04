using CRM.Client.Models;
using CRM.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Services
{
    public interface IHeaderService
    {
       

        Task<PageHeaderModel> Create(PageModality pageModality = PageModality.Visualization);

        Task<List<BreadcrumbItem>> GetBreadCrumbFromCurrentUrlAsync();
    }
}
