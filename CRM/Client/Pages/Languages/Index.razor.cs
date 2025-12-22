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

namespace CRM.Client.Pages.Languages
{
    public partial class Index : ComponentBase
    {

        private const string PageFolder = "Settings/Languages";

        private int _currentPage = 1;

        [Inject]
        private NavigationManager NavigationManager { get; set; }



        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnClickGantt { get; set; }

        private SfGrid<Language> GridLanguages;

        Language _language;

        private string _messageDelete;



        public void CommandClickHandler(CommandClickEventArgs<Language> args)
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

        public void ActionComplete(ActionEventArgs<Language> Args)
        {
            StateHasChanged();
        }

        protected void NewItem()
        {
            NavigationManager.NavigateTo($"/{PageFolder}/Edit");
        }

        

        protected async Task Delete()
        {

            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_language != null)
            {

                
                await RestClientService.Delete<int>(_language.Id, ConstHelper.LanguagesPath);

                GridLanguages.Refresh();
                StateHasChanged();
            }
        }

        protected void PrepareDelete(Language item)
        {
            _language = item;
            _messageDelete = $"Eliminare definitivamente la Lingua: {_language.Name}";


            StateHasChanged();
            JSRuntime.InvokeVoidAsync("ShowModal", "dlgDelete");

        }

        protected void Details(int id)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(id);
            }
            else
                NavigationManager.NavigateTo($"/{PageFolder}/Details/{id}");
        }

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/{PageFolder}/Edit/{id}");
        }
    }
}
