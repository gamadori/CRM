using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using Syncfusion.Blazor.Grids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Pages.LogEvents
{
    public partial class Index : ComponentBase
    {


        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnClickGantt { get; set; }


        private PagingHeaderModel _paging = new PagingHeaderModel();
        private string _messageDelete;


        LogEvent _logEvent;

        public void CommandClickHandler(CommandClickEventArgs<LogEvent> args)
        {
            switch (args.CommandColumn.Type)
            {
                case CommandButtonType.Delete:
                    PrepareDelete(args.RowData);
                    break;

                case CommandButtonType.Edit:
                    if (args.RowData != null)
                        Edit(args.RowData.Id);
                    break;

                case CommandButtonType.None:
                    if (args.RowData != null)
                    {                        
                        Details(args.RowData.Id);
                    }
                    break;
            }
        }

        public void CommandDetailsClick()
        {

        }
        public void ActionFailure(FailureEventArgs args)
        {
            //_errorDetails = args.Error.Message;
            StateHasChanged();
        }


        protected void NewItem()
        {
            NavigationManager.NavigateTo("/Projects/Edit");
        }

        public void ActionBegin(ActionEventArgs<LogEvent> Args)
        {
            if (Args.RequestType == Syncfusion.Blazor.Grids.Action.FilterChoiceRequest)
            {

            }
        }

        protected async Task Delete()
        {

            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_logEvent != null)
            {
                await Http.DeleteAsync($"{ConstHelper.LogEventsPath}/{_logEvent.Id}");

                StateHasChanged();
                
            }
        }
        protected void PrepareDelete(LogEvent item)
        {
            _logEvent = item;
            _messageDelete = $"Eliminare definitivamente il log";

          
            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

        }

        protected void Details(int idProduct)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idProduct);
            }
            else
                NavigationManager.NavigateTo($"/LogEvents/Details/{idProduct}");
        }


        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/LogEvents/Edit/{id}");
        }

    }
}
