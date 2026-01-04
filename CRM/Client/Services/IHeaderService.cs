using CRM.Client.Models;
using CRM.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Services
{
    public interface IHeaderService
    {
        PageHeaderModel Create(string domine, object? id = null, string? name = null, bool edit = false, string urlbase = null, string? subTitle = null,
           PageModality pageModality = PageModality.Visualization);

        Task<PageHeaderModel> Create(PageModality pageModality = PageModality.Visualization);

        Task<List<BreadcrumbItem>> GetBreadCrumbFromCurrentUrlAsync();
    }
}
