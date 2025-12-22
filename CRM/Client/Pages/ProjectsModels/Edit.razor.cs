using CRM.Client.Helpers;
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

namespace CRM.Client.Pages.ProjectsModels
{
    [Authorize]
    public partial class Edit: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

         
        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickSave { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private ProjectModel _task = null;

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.ProjectModelsPath;

                if (Id != null)
                {
                    path += $"/{Id}";

                    _task = await Http.GetFromJsonAsync<ProjectModel>(path);
                }
                else
                    _task = new ProjectModel();
                
               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        protected async Task HandleValidSubmit()
        {
            HttpResponseMessage resp;

            try
            {
                if (_task != null && _task.Id > 0)
                    resp = await Http.PutAsJsonAsync<ProjectModel>($"{ConstHelper.ProjectModelsPath}/{_task.Id}", _task);
                else
                    resp = await Http.PostAsJsonAsync<ProjectModel>(ConstHelper.ProjectModelsPath, _task);

                if (OnClickSave != null)
                    OnClickSave();
                else
                    NavigationManager.NavigateTo("/ProjectsModels/Index");
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
                NavigationManager.NavigateTo("/ProjectsModels/Index");
        }

        protected void Delete()
        {

        }

    }
}
