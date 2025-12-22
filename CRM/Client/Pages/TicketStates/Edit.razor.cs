using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.TicketStates
{
    [Authorize]
    public partial class Edit : ComponentBase
    {

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        
        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        [Parameter]
        public int? Id { get; set; }


        private TicketState _state = null;

        private List<TicketState> _ticketStates = new List<TicketState>();


        private string _messageState = "";

        private string _header = "Ticket Stati";

        private List<ItemList<int>> _stateList = new List<ItemList<int>>();

        

        protected override async Task OnInitializedAsync()
        {
            try
            {

                
                if (Id != null)
                {
                    
                    _header = "Modifica Ticket State";
                    _state = await RestClientService.GetItem<TicketState, int>(Id.Value, ConstHelper.TicketStatesPath);

                    await SelectDataSource(_state.State);
                }
                else
                {
                    
                    _header = "Nuovo Ticket State";
                    _state = new TicketState();
                    await SelectDataSource(null);
                }

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
                var resp = await RestClientService.Post<TicketState, int>(_state, ConstHelper.TicketStatesPath);

                if (resp != null && resp.State)
                {
                    if (OnClickSave != null)
                        OnClickSave();
                    else
                        NavigationManager.NavigateTo("/TicketStates");
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
                NavigationManager.NavigateTo("/TicketStates");
        }

        void Change(string value, string name)
        {

        }

        void Error(UploadErrorEventArgs args, string name)
        {

        }

        private async Task SelectDataSource(int? state)
        {

            var items = await RestClientService.GetListPag<TicketStateFilter, TicketState>(new TicketStateFilter(), ConstHelper.TicketStatesPath); 

            _stateList.Clear();
            var states = Enum.GetValues(typeof(eTicketStates));

            foreach (var s in Enum.GetValues(typeof(eTicketStates)))
            {
                if (!items.Items.Where(x => x.State == (int)s).Any() || (state != null && (int)s == state.Value))
                    _stateList.Add(new ItemList<int>() { Id = (int)s, Text = UtilityHelper.GetDisplayName<eTicketStates>((eTicketStates)s) });
            }
        }

    }
}
