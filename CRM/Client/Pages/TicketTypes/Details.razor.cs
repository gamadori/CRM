using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Client.Shared;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static CRM.Client.Helpers.PageHelper;

namespace CRM.Client.Pages.TicketTypes
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private ITicketTypesService _service { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }  

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private TicketType _ticketType = null;

        private PageHeaderModel _pageHeader = new PageHeaderModel();    

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.TicketTypesPath;

                if (Id != null)
                {
                    _ticketType = await _service.Get(Id.Value);

                }
                else
                    _ticketType = new TicketType();

                _pageHeader = await HeaderService.Create(PageMode);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

     

        protected void EditTicketType()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Settings/TicketTypes/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/Settings/TicketTypes/Index");
        }

       
    }
}
