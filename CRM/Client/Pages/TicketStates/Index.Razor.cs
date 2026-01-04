using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazoringComponents;
using Radzen;

namespace CRM.Client.Pages.TicketStates
{
    [Authorize]
    public partial class Index: ComponentBase
    {
        

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        
        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject] 
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        private INavMenuService navMenuService { get; set; }

        
        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }
        
        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public string MessagePrepareDelete { get; set; }

        [Parameter]
        public bool CmdDetails { get; set; } = true;

        [Parameter]
        public bool CmdEdit { get; set; } = true;

        [Parameter]
        public bool CmdDelete { get; set; } = true;

        


        private IQueryable<TicketState> _states = null;



        private PagingHeaderModel _paging = new PagingHeaderModel();

        private TicketStateFilter _filter = new TicketStateFilter();

        private string _messageDelete = "";

        private string _header;

        private TicketState _ticketState ;



        protected override void OnInitialized()
        {
            navMenuService.CallRequestRefresh();
            StateHasChanged();
        }

        public async Task<IEnumerable<TicketState>> LoadData()
        {
            var template = Enumerable.Empty<TicketState>().AsQueryable();
            try
            {
                
                _header = "Ticket Stati";


                var pagingResponse = await RestClientService.Get<TicketState, TicketStateFilter>(_filter, ConstHelper.TicketStatesPath);

                _states = pagingResponse.Items.AsQueryable();
                _paging = pagingResponse.MetaData;

                template = _states;
                
                return template;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);
                return template;
            }
            finally
            {
                
            }
     
        }


        protected void OnChangeDescriptionsFilter(ChangeEventArgs args)
        {
            _filter.Description = args.Value.ToString();
            StateHasChanged();
        }

        protected void Details(int idTicketState)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idTicketState);
            }
            else
                NavigationManager.NavigateTo($"/TicketStates/Details/{idTicketState}");
        }

        

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/TicketStates/{id}/Edit");
        }
        protected void Cancel()
        {
            NavigationManager.NavigateTo("/TicketStates");
        }
        protected void NewItem()
        {
            if (OnClickEdit != null)
                OnClickEdit(null);
            else
                NavigationManager.NavigateTo("/TicketStates/Edit");
        }

        protected async Task Delete()
        {
           
            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_ticketState != null)
            {
                if (OnClickDelete != null)
                    OnClickDelete(_ticketState.Id);
                else
                {
                    await RestClientService.Delete<int>(_ticketState.Id, ConstHelper.TicketStatesPath);

                    StateHasChanged();
                    //await LoadData();
                }
            }
        }

        protected void PrepareDelete(TicketState item)
        {
            _ticketState = item;

           
            if (MessagePrepareDelete != null && MessagePrepareDelete.Length > 0)
                _messageDelete = string.Format(MessagePrepareDelete, $"{ _ticketState.Description}");
            else
                _messageDelete = $"Eliminare definitivamente il Tipo di : {_ticketState.Description}";

            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

        }

        void OnChangeCompany(object value, string name)
        {
            var str = value;
        }
    }
}
