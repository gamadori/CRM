using CRM.Client.Helpers;
using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Radzen;
using Radzen.Blazor;
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
    public partial class Edit : ComponentBase
    {
      
        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Inject]
        ITicketTypesService _service { get; set; }

        [Inject]
        IHeaderService HeaderService { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public PageModality PageMode { get; set; } = PageModality.Visualization;

        private TicketType _state = null;

        private List<TicketType> _ticketStates = new List<TicketType>();

        private string _messageState = "";


        private PageHeaderModel? _pageHeader = null;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                if (Id != null)
                {
                    _state = await _service.Get(Id.Value);
                }
                else
                {
                    _state = new TicketType();
                }
                _pageHeader = await HeaderService.Create();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            _messageState = "";
            try
            {

                if (await _service.Post(_state) != null)
                {
                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo("/Settings/TicketTypes");
                }
                else
                    _messageState = "Errore durante il salvataggio";
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
        }

        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
                NavigationManager.NavigateTo("/Settings/TicketTypes");
        }

        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }

    }
}
