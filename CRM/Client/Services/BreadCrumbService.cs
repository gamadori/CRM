using CRM.Client.Helpers;
using CRM.Client.Shared;
using CRM.Shared;
using CRM.Shared.Helper;
using MediatR;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Linq;
using QLNet;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    public class BreadCrumbService : IBreadCrumbService
    {
        private readonly IStringLocalizer<CRM.Shared.Resources.App> _localizer;

        private readonly IUserService _userService;

        
       

        public BreadCrumbService(IStringLocalizer<CRM.Shared.Resources.App> localizer, IUserService userRestService)
        {
            _localizer = localizer;
            _userService = userRestService;
        }


        
        public async Task<List<BreadcrumbModel>> Settings(bool link = true)
        {
            List<BreadcrumbModel> bread = new List<BreadcrumbModel>();
            bread.AddRange(await Home());
            bread.Add(new BreadcrumbModel() { Title = _localizer["Settings"], Url = link ? "Settings" : null });

            return bread;
        }
        public async Task<List<BreadcrumbModel>> SMTP(bool link = false)
        {
            var bread = await Settings(true);
            bread.Add(new BreadcrumbModel() { Title = _localizer["SMTP"], Url = link ? _localizer["SMTP"] : null  });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> ProductSettings(bool link = true)
        {
            List<BreadcrumbModel> bread = await Settings();


            bread.Add(new BreadcrumbModel() { Title = _localizer["Product Settings"], Url = link ? ConstHelper.ClientProductsSettings : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> AzureSettings(bool link = true)
        {
            List<BreadcrumbModel> bread = await Settings();


            bread.Add(new BreadcrumbModel() { Title = _localizer["Azure Settings"], Url = link ? ConstHelper.ClientAzurePath : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> Home()
        {
            var user = await _userService.CurrentUser();

            
            if (user.IsClient)
            {
                return new List<BreadcrumbModel>() { new BreadcrumbModel() { Title = "Home", Url = "DashBoardClient" } };
            }
            else
               return new List<BreadcrumbModel>() { new BreadcrumbModel() { Title = _localizer["Home"], Url = "/" } };
        }

        public List<BreadcrumbItem> Ticket(string? idUser = null, Func<object, Task> action = null, object param = null)
        {


            var bread = new List<BreadcrumbItem>();
            List<BreadcrumbItem> breadcrumbItems = new()
            {
                new BreadcrumbItem(_localizer["Ticket"], $"Tickets/Index/{(int)TicketTypeSearch.All}/{idUser}", action, param)
            };

           

            return bread;
        }

        public List<BreadcrumbItem> TicketAssigned(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null)
        {
            string? url = null;
            
            List<BreadcrumbItem> model = Ticket(idUserAssigned, action, param);

            if (typeSearch != TicketTypeSearch.All)
            {
                
                url = $"/Tickets/Index/{(int)typeSearch}/{idUserAssigned}";

                model.Add(new BreadcrumbItem() {  Text =$"{_localizer[typeSearch.ToString()]}", Url = url });

                
            }

            return model;
        }

        public List<BreadcrumbItem> TicketFiltered(string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null)
        {
            string? url = null;

            List<BreadcrumbItem> model = Ticket(idUserAssigned, action, param);

            if (typeSearch != TicketTypeSearch.All)
            {
                
                url = $"/Tickets/Index/{(int)typeSearch}/{idUserAssigned}";
                model.Add(new BreadcrumbItem() { Text = $"{_localizer[typeSearch.ToString()]}", Url = url });


            }

            return model;
        }

        public List<BreadcrumbItem> TicketNumber(int idTicket, string? idUserAssigned = null, TicketTypeSearch typeSearch = TicketTypeSearch.All, Func<object, Task> action = null, object param = null)
        {
            string? url = null;

            List<BreadcrumbItem> model = Ticket(idUserAssigned, action, param);

            if (typeSearch != TicketTypeSearch.All)
            {
                
                url = $"/Tickets/Index/{(int)typeSearch}/{idUserAssigned}";
                model.Add(new BreadcrumbItem() { Text = $"{_localizer[typeSearch.ToString()]}", Url = url });


            }
            model.Add(new BreadcrumbItem() { Text = $"{_localizer["Ticket n"]} {idTicket}", Url = null });
            return model;
        }

        public async Task<List<BreadcrumbModel>> Deal(bool link)
        {
            var bread = await Home();

            bread.Add(new BreadcrumbModel() { Title = _localizer["Deal"], Url = (link) ? "Deals/Index" : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> DealUser(string idUser, bool link)
        {

            List<BreadcrumbModel> model = await Deal(idUser != null ? true : false);
            if (idUser != null) 
            {
                var user = await _userService.GetItem<ApplicationUser, string>(idUser, ConstHelper.UsersPath);
                if (user != null)
                {
                    model.Add(new BreadcrumbModel() { Title = $"{_localizer["Owner To"]} {user.NameComplete}", Url = link ?  $"/Deal/Index/{idUser}" : null });
                }
            }
            return model;
        }

        public async Task<List<BreadcrumbModel>> Projects(bool link)
        {
            var bread = await Home();

            bread.Add(new BreadcrumbModel() { Title = _localizer["Projects"], Url = link ? "Projects/Index" : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> ContractTypes(bool link, string? txt = null)
        {
            var bread = await Settings();

            bread.Add(new BreadcrumbModel() { Title = _localizer["ContractTypes"], Url = link || txt != null ? $"{ConstHelper.ClientContractTypesPath}/Index" : null });

            if (txt != null)
                bread.Add(new BreadcrumbModel() { Title = txt, Url = null });


            return bread;
        }

        

        public async Task<List<BreadcrumbModel>> AccessoryTypes(bool link)
        {
            var bread = await ProductSettings();

            bread.Add(new BreadcrumbModel() { Title = _localizer["Accessory Types"], Url = link ? ConstHelper.ClientAccessoryTypesPath : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> AccessoryTypes(string accessoryType, bool link)
        {
           
            var bread = await AccessoryTypes(true);

            bread.Add(new BreadcrumbModel() { Title = accessoryType, Url = link ? ConstHelper.ClientAccessoryTypesPath : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> Accessories(bool link)
        {

            var bread = await ProductSettings();

            bread.Add(new BreadcrumbModel() { Title = _localizer["Accessories"], Url = link ? ConstHelper.ClientAccessoriesPath : null });

            return bread;
        }

        public async Task<List<BreadcrumbModel>> ProductTypes(bool link)
        {
            var bread = await Home();

            bread.Add(new BreadcrumbModel() { Title = _localizer["Product Types"], Url = link ? ConstHelper.ClientProductAccTypesPath : null });   
            
            return bread;
        }

        
        public async Task<List<BreadcrumbModel>> ProductTypeAccs(bool link)
        {
            var bread = await ProductTypes(true);

            bread.Add(new BreadcrumbModel() { Title = _localizer["Accessory"], Url = link ? ConstHelper.ClientProductAccTypesPath : null });
            return bread;
        }


        public List<BreadcrumbItem> Companies(int? id = null, string? name = null)
        {
            List<BreadcrumbItem> breadcrumbItems = new()
            {
                new BreadcrumbItem(_localizer["Companies"], "/Companies"),
            };
            if (id != null)
            {
                breadcrumbItems.Add(new BreadcrumbItem(name, $"/Articles/{id}"));
            }
            return breadcrumbItems; 
        }

        
      

        public List<BreadcrumbItem> Contacts(int? id = null, string name = null, bool edit = false)
        {

            List< BreadcrumbItem> breadcrumbItems = new()
            {
                new BreadcrumbItem(_localizer["Contacts"], ConstHelper.ContactsPath),
            };


            if (id != null)
            {
                breadcrumbItems.Add(new BreadcrumbItem()
                {
                    Text = name,
                    Url = $"{ConstHelper.ContactsPath}/{id}/Details"
                });
            }
            else if (edit)
            {
                breadcrumbItems.Add(new BreadcrumbItem()
                {
                    Text = _localizer["New Contact"],
                    Url = null
                });
            }

            return breadcrumbItems;
        }

        

    }
    

}
