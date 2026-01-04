using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Projects
{
    public partial class Index : ComponentBase
    {


        [Inject]
        private NavigationManager NavigationManager { get; set; }


        [Inject]
        IAGRestClientService RestClientService { get; set; }


        [Inject]
        private IJSRuntime JSRuntime { get; set; }

        [Inject]
        DialogService DialogService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Inject]
        IBreadCrumbService BreadCrumbService { get; set; }


        [Parameter]
        public Action<int> OnClickDetails { get; set; }

        [Parameter]
        public Action<int?> OnClickEdit { get; set; }

        [Parameter]
        public Action<int> OnClickDelete { get; set; }

        [Parameter]
        public Action<int> OnClickGantt { get; set; }

        private string _pagingSummaryFormat;

        private List<Project> _projects = null;

        private ProjectFilter _filter = new ProjectFilter(); // { Top = ConstHelper.PageSize, Skip = 0 };

        private RadzenDataGrid<Project> grdProjects;

        private PagingHeaderModel _paging = new PagingHeaderModel();

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        private bool _isLoading = false;

        protected override async Task OnInitializedAsync()
        {


            _isLoading = true;

            _bread = await BreadCrumbService.Projects(false);

            _pagingSummaryFormat = Localize["Displaying page {0} of {1} (total {2} records)"];

            await LoadData();
        }

        public async Task LoadData(LoadDataArgs args = null)
        {

            _isLoading = true;

            var template = Enumerable.Empty<Project>().AsQueryable();

            try
            {
                if (args != null)
                {
                    _filter.Skip = args?.Skip;
                    _filter.Top = args?.Top;
                    _filter.Filter = args?.Filter;
                    _filter.OrderBy = args?.OrderBy;
                }
                var pagingResponse = await RestClientService.Get<Project, ProjectFilter>(_filter, ConstHelper.ProjectsPath);

                _projects = pagingResponse.Items;
                _paging = pagingResponse.MetaData;
            }

            catch (Exception ex)
            {
                Console.WriteLine(ex);

            }
            finally
            {
                _isLoading = false;
                await InvokeAsync(StateHasChanged);
            }

        }



        protected void NewItem()
        {
            NavigationManager.NavigateTo("/Projects/New");
        }

       

        protected async Task Delete(Project item)
        {
          

            if (item != null)
            {
                if (await DialogService.Confirm(string.Format(Localize["Eliminare il progetto{0}?"], item.Name)) == true)
                {
                    await RestClientService.Delete<int>(item.Id, ConstHelper.ProjectsPath);
                }
            }
            
            StateHasChanged();

        }
        
        protected void Details(int idProject)
        {
            if (OnClickDetails != null)
            {
                OnClickDetails(idProject);
            }
            else
                NavigationManager.NavigateTo($"/Projects/{idProject}");
        }


        protected void Edit(int id)
        {
            if (OnClickEdit != null)
                OnClickEdit(id);
            else
                NavigationManager.NavigateTo($"/Projects/{id}/Edit");
        }

    }
}
