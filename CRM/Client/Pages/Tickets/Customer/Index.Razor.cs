using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Tickets.Customer
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        HttpClient HttpClient { get; set; }

        [Inject]
        private IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int TypeSearch { get; set; } = (int)TicketTypeSearch.All;

        private PagingResponse<Ticket> _tickets = null;

        private bool _isLoading = false;

        private RadzenDataGrid<Ticket> grdTickets;

        private string _header = "Tickets";

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>()  { new BreadcrumbModel(){ Title = "Home", Url = "DashBoardClient" }};

        protected async override Task OnInitializedAsync()
        {
            //#if DEBUG
            //            await Task.Delay(10000);
            //#endif

            //navMenuService.CallRequestRefresh();
            _header = Header();

            _bread.Add(new BreadcrumbModel() { Title = _header, Url = null });
            await LoadData();
        }

        
        public async Task LoadData(LoadDataArgs args = null)
        {
            TicketFilter paging = new TicketFilter() { PageSize = 10, Skip = 0, Top = 10 }; ;
            _isLoading = true;

            try
            {
                
                if (args != null)
                {
                    paging.Skip = args.Skip;
                    paging.Top = args.Top;
                    paging.OrderBy = args.OrderBy;


                }
                paging.TypeSearch = TypeSearch;

                if (args.Filters != null && args.Filters.Any())
                {
                    if (paging.Filter?.Length > 0)
                        paging.Filter += " And ";
                    paging.Filter += args.Filter;
                }
        
                _tickets = await RestClientHelper.Get<Ticket>(HttpClient, ConstHelper.TicketPath, paging);

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {

                if (_tickets == null)
                {
                    _tickets = new PagingResponse<Ticket>();
                    _tickets.Items = new List<Ticket>();
                    _tickets.MetaData = new PagingHeaderModel();
                }
                _isLoading = false;
            }

        }

        private void Details(int id)
        {

        }


        private string Header()
        {
            switch ((TicketTypeSearch)TypeSearch)
            {
                case TicketTypeSearch.Working:
                    return Localize["Tickets in Lavorazioni"];

                case TicketTypeSearch.Closed:
                    return Localize["Tickets Chiusi"];

            }
            return "Tickets";
        }
    }
}
