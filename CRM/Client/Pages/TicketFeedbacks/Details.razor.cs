using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
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

namespace CRM.Client.Pages.TicketFeedbacks
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        ITicketFeedbackService Service { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

       

        [Parameter]
        public int Id { get; set; }

        
        [Parameter]
        public Action<int>OnClickEditChild { get; set; } 

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private TicketFeedbackResponse _item = null;

        private PageHeaderModel? _pageHeader = null;
        protected override async Task OnInitializedAsync()
        {
            try
            {
                _item = await Service.GetItemAsync(Id);   // _service.Get(Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/TicketFeedbacks/Index");
        }


    }
}
