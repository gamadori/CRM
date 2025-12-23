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

        Task<List<BreadcrumbModel>> Ticket(string? idUser = null, Func<object, Task> action = null, object param = null);

        Task<List<BreadcrumbModel>> TicketAssigned(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, bool url = false, Func<object, Task> action = null, object param = null);

        Task<List<BreadcrumbModel>> TicketFiltered(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, bool link = false, Func<object, Task> action = null, object param = null);

        Task<List<BreadcrumbModel>> TicketNumber(int idTicket, string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null);

        Task<List<BreadcrumbModel>> Talk(bool link);

        Task<List<BreadcrumbModel>> TalkUser(string idUser, bool link);

        Task<List<BreadcrumbModel>> Projects(bool link);

        Task<List<BreadcrumbModel>> AccessoryTypes(bool link);

        Task<List<BreadcrumbModel>> AccessoryTypes(string accessoryType, bool link);

        Task<List<BreadcrumbModel>> Accessories(bool link);

        Task<List<BreadcrumbModel>> ProductTypeAccs(bool link);

        Task<List<BreadcrumbModel>> ContractTypes(bool link, string? txt = null);

        public List<BreadcrumbItem> Companies(int? id = null, string? name = null);

        Task<List<BreadcrumbModel>> Contacts(string name = null, bool link = false);

        Task<List<BreadcrumbModel>> Articles(string name = null, bool link = false);
    }
}
