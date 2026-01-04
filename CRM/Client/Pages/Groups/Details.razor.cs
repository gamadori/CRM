using CRM.Client.Helpers;
using CRM.Client.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.Groups
{
    [Authorize]
    public partial class Details: ComponentBase
    {
        [Inject]
        private HttpClient Http { get; set; }

        [Inject]
        private NavigationManager NavigationManager { get; set; }

        [Inject]
        IAGRestClientService RestClientService { get; set; }

        [Inject]
        IStringLocalizer<CRM.Shared.Resources.App> Localize { get; set; }

        [Parameter]
        public int? Id { get; set; }

        [Parameter]
        public Action OnClickEdit { get; set; }

        [Parameter]
        public Action OnClickCancel { get; set; }

        private Group _group = null;

        private List<BreadcrumbModel> _bread = new List<BreadcrumbModel>();

        protected override async Task OnInitializedAsync()
        {
            string path;
            try
            {
                //await Task.Delay(10000);      // changes are flushed again   
                path = ConstHelper.GroupsPath;

                _bread.Add(new BreadcrumbModel() { Title = Localize["Settings"], Url="Settings" });
                _bread.Add(new BreadcrumbModel() { Title = Localize["Gruppi"], Url = "Settings/Groups" });

                if (Id != null)
                {
                    _group = await RestClientService.GetItem<Group, int>(Id.Value, ConstHelper.GroupsPath); 
                    _bread.Add(new BreadcrumbModel() { Title = _group.Name, Url = null });

                }
                else
                {
                    _group = new Group();
                    _bread.Add(new BreadcrumbModel() { Title = Localize["Nuovo Gruppo"], Url = null });
                }

               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

     

        protected void EditGroup()
        {
            if (OnClickEdit != null)
                OnClickEdit();
            else
                NavigationManager.NavigateTo($"/Settings/Groups/{Id}/Edit");
        }
        protected void Annulla()
        {
            if (OnClickCancel != null)
                OnClickCancel();
            else
             NavigationManager.NavigateTo("/Settings/Groups/Index");
        }

        protected void SendInvitation()
        {

        }

    }
}
