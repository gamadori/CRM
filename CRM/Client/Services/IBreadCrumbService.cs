using CRM.Client.Shared;
using CRM.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public interface IBreadCrumbService
    {
        Task<List<BreadcrumbModel>> Home();

        Task<List<BreadcrumbModel>> Settings(bool link = true);

        Task<List<BreadcrumbModel>> ProductSettings(bool link = true);

        Task<List<BreadcrumbModel>> AzureSettings(bool link = true);

        Task<List<BreadcrumbModel>> SMTP (bool link = false);

        List<BreadcrumbItem> Ticket(string? idUser = null, Func<object, Task> action = null, object param = null);

        List<BreadcrumbItem> TicketAssigned(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null);

        List<BreadcrumbItem> TicketFiltered(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All,  Func<object, Task> action = null, object param = null);

        List<BreadcrumbItem> TicketNumber(int idTicket, string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null);

        Task<List<BreadcrumbModel>> Deal(bool link);

        Task<List<BreadcrumbModel>> DealUser(string idUser, bool link);

        Task<List<BreadcrumbModel>> Projects(bool link);

        Task<List<BreadcrumbModel>> AccessoryTypes(bool link);

        Task<List<BreadcrumbModel>> AccessoryTypes(string accessoryType, bool link);

        Task<List<BreadcrumbModel>> Accessories(bool link);

        Task<List<BreadcrumbModel>> ProductTypeAccs(bool link);

        Task<List<BreadcrumbModel>> ContractTypes(bool link, string? txt = null);

        public List<BreadcrumbItem> Companies(int? id = null, string? name = null);

        List<BreadcrumbItem> Contacts(int? id = null, string name = null, bool edit = false);

       
       
    }
}
