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

namespace CRM.Client.Pages.ProjectsModels
{
    public partial class Index : ComponentBase
    {

        private int _currentPage = 1;

        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }


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



        private SfGrid<ProjectModel> GridProjects;

        private List<ProjectModel> _tasks = null;
        private ProjectModelFilter _filter = new ProjectModelFilter();
        ProjectModel _project;

        private PagingHeaderModel _paging = new PagingHeaderModel();
        private string _messageDelete;

        

        

        //private string _errorDetails = "";
        public async Task GetTasks()
        {
            try
            {

                Dictionary<string, string> param = new Dictionary<string, string>();

                param.Add(nameof(_filter.Name), _filter.Name);
                param.Add(nameof(_filter.Description), _filter.Description);

                var qs = UriHelper.BuildQueryString(param);

                var response = await RestClientService.Get<ProjectModel, ProjectModelFilter>(_filter, ConstHelper.ProjectModelsPath);

                if (response != null)
                {


                    _tasks = response.Items;
                    _paging = response.MetaData;
                }

            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine(ex.Message, ex);

            }

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ex);

            }
        }

        
        public void CommandClickHandler(CommandClickEventArgs<ProjectModel> args)
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
                        if (args.CommandColumn.Title == "Gantt")
                            Gantt(args.RowData.Id);
                        else
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
            NavigationManager.NavigateTo("/ProjectsModels/New");
        }

        public void ActionBegin(ActionEventArgs<ProjectModel> Args)
        {
            if (Args.RequestType == Syncfusion.Blazor.Grids.Action.FilterChoiceRequest)
            {

            }
        }

        protected async Task Delete()
        {

            await JSRuntime.InvokeAsync<object>("CloseModal", "dlgDelete");

            if (_project != null)
            {

                await RestClientService.Delete<int>(_project.Id, ConstHelper.ProjectModelsPath);

                GridProjects.Refresh();
                StateHasChanged();
            }
        }
        protected void PrepareDelete(ProjectModel item)
        {
            _project = item;
            _messageDelete = $"Eliminare definitivamente il Progetto: {_project.Name}";

            
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
                NavigationManager.NavigateTo($"/ProjectsModels/Details/{idProduct}");
        }

        protected void Gantt(int idProduct)
        {
            if (OnClickGantt != null)
            {
                OnClickGantt(idProduct);
            }
            else
                NavigationManager.NavigateTo($"/TasksProjectsModels/Index/{idProduct}");
        }

        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/ProjectsModels/{id}/Edit");
        }

    }
}
